# Mobile tooling setup (owner, macOS)

Written 23 October 2026 for the owner's Apple Silicon Mac (D64: .NET 10 + MAUI, built and tested locally). It lists what is still missing after the overnight slice E and the exact commands to install it. Nothing here was installed overnight: the commands download several GB, change system locations or need administrator rights.

## What this Mac already has (checked 23 October 2026)

| Item | State |
| --- | --- |
| .NET SDK | 10.0.400 (pinned by `global.json`) |
| `maui` workload | 10.0.110 installed (`dotnet workload list`) |
| Xcode | 27.1 (27A9275); iOS 27.0 simulator runtime present |
| Android SDK | **Missing** overnight; installed by the owner later on 23 October 2026 (see [validation](VALIDATION.md), slice E) |
| Java (JDK) | **Missing** overnight; `InstallAndroidDependencies` then installed Microsoft OpenJDK 17.0.14 |

See [validation](VALIDATION.md) (slice E) for what built with this setup.

## 1. Confirm the workload and Xcode

```sh
dotnet workload list            # expect: maui 10.0.110 (or later) for SDK 10.0.400
xcode-select -p                 # expect: /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -license accept # once per Xcode version
xcrun simctl list runtimes      # expect an iOS runtime
```

If `maui` is missing or older, install or update it. On macOS this needs administrator rights:

```sh
sudo dotnet workload install maui
# or, to update an existing install:
sudo dotnet workload update
```

## 2. Android SDK and JDK (one command)

Microsoft's recommended route is the `InstallAndroidDependencies` build target. It installs the Android SDK components this project needs and, with `JavaSdkDirectory`, a Microsoft OpenJDK build tested with .NET for Android. Use absolute paths without `~` or spaces. Run it from the repository root:

```sh
dotnet build src/Swoovr.Mobile -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory="$HOME/Library/Android/sdk" \
  -p:JavaSdkDirectory="$HOME/Library/Developer/Java/jdk" \
  -p:AcceptAndroidSDKLicenses=True
```

This accepts the Android SDK licences on your behalf (`AcceptAndroidSDKLicenses=True`); read them first if you prefer (`sdkmanager --licenses`). Then add to `~/.zshrc`:

```sh
export ANDROID_HOME="$HOME/Library/Android/sdk"
export JAVA_HOME="$HOME/Library/Developer/Java/jdk"
```

Alternative: install [Microsoft OpenJDK](https://learn.microsoft.com/java/openjdk/download) (the .NET for Android docs name OpenJDK 21) and Android Studio, then point `JAVA_HOME` and `ANDROID_HOME` at them.

## 3. Android emulator (Apple Silicon)

```sh
cd "$ANDROID_HOME/cmdline-tools/latest/bin"   # the folder name may be a version number
./sdkmanager --install emulator "system-images;android-35;google_apis;arm64-v8a"
./avdmanager create avd -n swoovr-pixel -k "system-images;android-35;google_apis;arm64-v8a" -d pixel_7
```

Pick the system image API level that matches the Android target the build reports (D64 rule 9: current MAUI and Google Play requirements). API 35 is Microsoft's documented example, not a decision.

## 4. Build and run

```sh
dotnet build src/Swoovr.Mobile -f net10.0-ios       # iOS simulator build (no signing needed)
dotnet build src/Swoovr.Mobile -f net10.0-android   # after steps 2–3
dotnet build src/Swoovr.Mobile -t:Run -f net10.0-ios
```

`-t:Run` for Android installs onto a device that is already running; it does not boot an emulator and fails with `XA0010: No available device` otherwise. Start the emulator first:

```sh
"$ANDROID_HOME/emulator/emulator" -avd swoovr-pixel &
"$ANDROID_HOME/platform-tools/adb" wait-for-device   # the emulator may still be booting after this returns
dotnet build src/Swoovr.Mobile -t:Run -f net10.0-android
```

If the install fails, wait for the Android home screen and run the last command again. The app targets API 36 (MAUI Android pack 36.1); the API 35 image runs it, and an API 36 image (`system-images;android-36;google_apis;arm64-v8a`) matches the target exactly.

A physical iPhone needs an Apple Developer account, a provisioning profile and the bundle identifier (`com.swoovr.app` is a DEFAULT, not registered). Those belong to the store and TestFlight work (D63, D64), not this setup.

## 5. Record the result

Add the versions you installed (`dotnet workload list`, `xcodebuild -version`, `java -version`, `sdkmanager --list_installed`) and the build results to [validation](VALIDATION.md), and pin the workload set once it builds (D64: SDK pinned by `global.json` plus a workload set).

## 5. Sign in against the local stack

Debug builds sign in to the local members realm (D93–D95). Start the stack and the hosts, then:

```sh
./scripts/docker-local.sh up
./scripts/docker-local.sh realms              # creates the swoovr-mobile client in an existing local realm
# Android: the emulator reaches the Mac's localhost through adb reverse (not 10.0.2.2, which would change the token issuer)
"$ANDROID_HOME/platform-tools/adb" reverse tcp:8180 tcp:8180
"$ANDROID_HOME/platform-tools/adb" reverse tcp:5080 tcp:5080
dotnet build src/Swoovr.Mobile -t:Run -f net10.0-android
# iOS simulator: shares the Mac's loopback, nothing to forward
dotnet build src/Swoovr.Mobile -t:Run -f net10.0-ios
```

`adb reverse` lasts until the emulator restarts. Tap **Sign in**; iOS asks "“Swoovr” Wants to Use “localhost” to Sign In" (tap Continue). Use a fictional member that is on the beta list (for example one created with `tests/auth/local-tokens.cjs` and `dotnet run --project src/Swoovr.Api -- beta add <email>`).

## 6. Profile, Explore and Travel tabs (slice N, 24 October 2026)

The app now has four tabs (D123). On iOS, `-t:Run` needs a plain build first (otherwise it fails with "The app must be built before the arguments to launch the app using mlaunch can be computed"), and it stays attached until the app exits:

```sh
dotnet build src/Swoovr.Mobile -f net10.0-ios
dotnet build src/Swoovr.Mobile -t:Run -f net10.0-ios -p:_DeviceName=:v2:udid=<simulator udid>   # or: xcrun simctl launch booted com.swoovr.app
```

Tap-through for iOS (not run by the builder): sign in on **Me** with a fictional beta member. On **Profile**, change the display name and save, type two letters in **Home city** and pick "Adelaide, South Australia, Australia", turn Dating on and pick chips until "5 of 5" (the rest go grey). On **Explore**, the four modes and nothing called Travel; a mode that's off says so; send a request. On **Travel**, add a trip (city search, arrival, departure), switch "Visible to locals" off and on, cancel it, and with an Adelaide home city check "Visiting Adelaide" lists travellers and Send request answers "Request sent". The GeoNames credit shows under each city search. Travel needs GeoNames data in the local database: `./scripts/geonames.sh download && ./scripts/geonames.sh import`.
