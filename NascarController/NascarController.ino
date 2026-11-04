#include <Arduino_MKRIoTCarrier.h>
#include <WiFiNINA.h>
#include <PubSubClient.h>
#include "secrets.h"

WiFiClient wifiClient;
PubSubClient mqtt(wifiClient);
MKRIoTCarrier carrier;
int r, g, b;
unsigned long lastPublish = 0;
const unsigned long PUBLISH_INTERVAL = 30;
char CarId[33];

void updateLights() {
  carrier.leds.clear();
  for (int i = 0; i < 5; i++) {
      carrier.leds.setPixelColor(i, 50, 0, 0);
    };
  carrier.leds.show();
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
  snprintf(CarId, sizeof(CarId), "%s", CarId);
  mqtt.publish(MQTT_TOPIC_NEW, CarId);
}
void loop() {
 if (WiFi.status() != WL_CONNECTED) {
        connectWiFi();
    }

    if (!mqtt.connected()) {
        connectMqtt();
        mqtt.publish(MQTT_TOPIC_NEW, CarId);
    }

static unsigned long lastLoopSuccess = millis();

if (!mqtt.loop()) {
    Serial.print("MQTT loop failed after ");
    Serial.print(millis() - lastLoopSuccess);
    Serial.println(" ms");
} else {
    lastLoopSuccess = millis();
}

     if (millis() - lastPublish >= PUBLISH_INTERVAL) {
        lastPublish = millis();

        float x, y, z;
        carrier.IMUmodule.readAcceleration(x, y, z);

        char payload[128];
        snprintf(payload, sizeof(payload),
         "{\"carId\":\"%s\",\"throttle\":%.3f,\"steering\":%.3f}", // to send z add this at the end - , \"z\":%.3f
         CarId, x, y); // z
         Serial.println(payload);

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
