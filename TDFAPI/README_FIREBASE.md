# Firebase Cloud Messaging Setup

This document provides instructions for setting up Firebase Cloud Messaging (FCM) for the TDF API.

## Prerequisites

1. A Google account
2. Access to the Firebase Console (https://console.firebase.google.com/)

## Setup Instructions

### 1. Create a Firebase Project

1. Go to the Firebase Console (https://console.firebase.google.com/)
2. Click "Add project"
3. Enter a project name (e.g., "TDF")
4. Follow the setup wizard to create the project

### 2. Register Your Android App

1. In the Firebase Console, select your project
2. Click "Add app" and select Android
3. Enter your package name: `com.TDF.TDFMAUI`
4. Enter your app nickname (optional)
5. Click "Register app"
6. Download the `google-services.json` file
7. Place the `google-services.json` file in the `TDFMAUI/Platforms/Android` directory

### 3. Generate a Service Account Key

1. In the Firebase Console, go to Project Settings
2. Go to the "Service accounts" tab
3. Click "Generate new private key"
4. Save the JSON file as `google-service-account.json`
5. Place the file in the root directory of the TDFAPI project

### 4. Configure Environment Variables (Optional)

You can set the path to your service account key using an environment variable:

```
GOOGLE_APPLICATION_CREDENTIALS=path/to/your/google-service-account.json
```

## Testing FCM

To test FCM, you can use the Firebase Console:

1. Go to the Firebase Console and select your project
2. Go to "Cloud Messaging"
3. Click "Send your first message"
4. Enter a notification title and text
5. Under "Target", select "Single device" and enter the FCM token
6. Click "Send message"

## Troubleshooting

### Common Issues

1. **Service account file not found**: Ensure the `google-service-account.json` file is in the correct location or set the `GOOGLE_APPLICATION_CREDENTIALS` environment variable.

2. **Invalid service account**: Make sure you've downloaded the correct service account key for your Firebase project.

3. **FCM token not registered**: Ensure the app has successfully registered with FCM and the token is stored in the database.

4. **Notification not appearing**: Check that the Android app has the necessary permissions and that the notification channel is properly configured.

## Additional Resources

- [Firebase Cloud Messaging Documentation](https://firebase.google.com/docs/cloud-messaging)
- [Firebase Admin SDK Documentation](https://firebase.google.com/docs/admin/setup)
- [MAUI Firebase Integration Guide](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/push-notifications)