#include <Arduino_MKRIoTCarrier.h>
#include <WiFiNINA.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include "secrets.h"

WiFiClient wifiClient;
PubSubClient mqtt(wifiClient);
MKRIoTCarrier carrier;
unsigned long lastPublish = 0;
const unsigned long PUBLISH_INTERVAL = 30;
char CarId[33];

void updateLapLeds(int lap) {
    lap = constrain(lap, 0, 10);
    for (int i = 0; i < 5; i++) {
        if (i < lap - 5) {
            carrier.leds.setPixelColor(i, 0, 64, 0);
        }
        else if (i < lap) {
            carrier.leds.setPixelColor(i,64, 45, 0);
        }
        else {
            carrier.leds.setPixelColor(i, 0, 0, 0);
        }
    }

    carrier.leds.show();
}
void displayName(String name){
  carrier.display.fillScreen(ST77XX_BLACK);
  carrier.display.setTextColor(ST77XX_WHITE);
  carrier.display.setTextSize(3);
  carrier.display.setRotation(1);
  int16_t x1, y1;
  uint16_t textWidth, textHeight;
  carrier.display.getTextBounds(
    name,
    0, 0,
    &x1, &y1,
    &textWidth, &textHeight
  );
  int x = (carrier.display.width() - textWidth) / 2 - x1;
  int y = (carrier.display.height() - textHeight) / 2 - y1;
  carrier.display.setCursor(x, y);
  carrier.display.println(name);
}
void callback(char* topic, byte* payload, unsigned int length)
{
  String content = "";
  for (size_t i = 0; i < length; i++)
  {
    content.concat((char)payload[i]);
  }
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, payload);
  if (error) {
    Serial.print("deserializeJson() failed: ");
    Serial.println(error.c_str());
    return;
  }
  if(strcmp(topic, LAPS_TOPIC) == 0 && doc["carId"] == CarId) {
    updateLapLeds(doc["content"]);
  }
    if(strcmp(topic, NAME_TOPIC) == 0 && doc["carId"] == CarId) {
      displayName(doc["content"].as<String>());
  }
}
void connectMqtt() {
    Serial.print("Wi-Fi status: ");
    Serial.println(WiFi.status());

    Serial.print("Connecting to MQTT... ");
    String clientId = String("Arduino-") + CarId;
    if (mqtt.connect(clientId.c_str(), MQTT_TOPIC_REMOVE, 0, true, CarId)) {
        Serial.println("connected");
    } else {
        Serial.print("failed, state: ");
        Serial.println(mqtt.state());
        delay(2000);
    }
}
void connectWiFi() {
    Serial.print("Connecting to Wi-Fi");

    WiFi.begin(WIFI_SSID, WIFI_PASS);

    while (WiFi.status() != WL_CONNECTED) {
        delay(500);
        Serial.print(".");
    }

    Serial.println("\nWi-Fi connected");
    Serial.println(WiFi.localIP());
}
String getDeviceId() {
  const uint32_t* serialAddress =
      reinterpret_cast<const uint32_t*>(0x0080A00C);

  uint32_t words[4] = {
    serialAddress[0],
    serialAddress[1],
    serialAddress[2],
    serialAddress[3]
  };

  char id[33];

  snprintf(
    id,
    sizeof(id),
    "%08lX%08lX%08lX%08lX",
    (unsigned long)words[0],
    (unsigned long)words[1],
    (unsigned long)words[2],
    (unsigned long)words[3]
  );

  return String(id);
}

void setup() {
  Serial.begin(115200);
  delay(1000);
  carrier.noCase();
  carrier.begin();
  String id = getDeviceId();
  id.toCharArray(CarId, sizeof(CarId));
  connectWiFi();
  mqtt.setServer(MQTT_SERVER, MQTT_PORT);
  mqtt.setKeepAlive(5);
  mqtt.setSocketTimeout(2);
  connectMqtt();
  mqtt.setCallback(callback);
  snprintf(CarId, sizeof(CarId), "%s", CarId);
  mqtt.publish(MQTT_TOPIC_NEW, CarId);
  mqtt.subscribe(NAME_TOPIC);
  mqtt.subscribe(LAPS_TOPIC);
}
void loop() {
 if (WiFi.status() != WL_CONNECTED) {
    connectWiFi();
  }
  if (!mqtt.connected()) {
    connectMqtt();
    mqtt.publish(MQTT_TOPIC_NEW, CarId);
  }
  mqtt.loop();
  if (millis() - lastPublish >= PUBLISH_INTERVAL) {
    lastPublish = millis();
    float x, y, z;
    carrier.IMUmodule.readAcceleration(x, y, z);
    char payload[128];
    snprintf(payload, sizeof(payload),
      "{\"carId\":\"%s\",\"throttle\":%.3f,\"steering\":%.3f}",
      CarId, x, y);
    if (!mqtt.connected()) {
      Serial.println("MQTT disconnected, state: ");
      Serial.println(mqtt.state());
      return;
    }
    if (mqtt.publish(MQTT_TOPIC_INPUT, payload)) {
    } else {
        Serial.println("MQTT publish failed");
        Serial.print("MQTT state: ");
        Serial.println(mqtt.state());
      }
  }
}
