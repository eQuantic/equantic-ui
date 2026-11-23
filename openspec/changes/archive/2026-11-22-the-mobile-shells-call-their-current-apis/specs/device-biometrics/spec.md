## ADDED Requirements

### Requirement: Android's biometric availability is a reader with something enrolled

`IBiometrics.IsAvailable` on Android SHALL ask the platform whether a biometric of the weak class or stronger
can authenticate, which excludes the device credential. The prompt SHALL allow a weak biometric or the device
credential.

#### Scenario: A PIN and no fingerprint

- **WHEN** the Android emulator has a PIN set and no fingerprint enrolled
- **THEN** `IsAvailable` is false, and the prompt that `AuthenticateAsync` shows accepts the PIN and answers
  `Succeeded`
