# Native UAT build configuration

D280 DEFAULT selects nonsecret endpoints at build time. Debug defaults to Development; Release defaults to Uat. UAT uses `https://api.uat.swoovr.app/` and `https://auth.uat.swoovr.app/realms/swoovr-members`, public client `swoovr-mobile`, and existing `com.swoovr.app:/oauth2redirect` / `com.swoovr.app:/signedout` callbacks. The label is **Swoovr UAT**. These domains follow D221’s reversible hosting default; deployment, DNS and store/signing registration are separate operator work.

Development endpoints require Debug. An explicit `SwoovrOwnedApiTest=true` additionally selects loopback5400 for the disposable emulator fixture, and is refused for UAT/Release. UAT disables Android cleartext, local iOS ATS exceptions, Debug logging and simulated push token generation. A provider-disabled UAT build says push is not enabled; old simulated tokens cannot be registered. Enabling FCM requires an external GoogleServicesJson file for any UAT build. UAT Release APNs requires production entitlements; provider credentials and physical device delivery remain unverified.

```sh
# Installed MAUI workloads and platform toolchains required.
node --test tests/mobile/build-config.test.cjs
dotnet build src/Swoovr.Mobile -f net10.0-android -c Release -p:SwoovrEnvironment=Uat -t:SignAndroidPackage -p:AndroidPackageFormats=apk -p:EmbedAssembliesIntoApk=true
dotnet build src/Swoovr.Mobile -f net10.0-ios -c Release -p:SwoovrEnvironment=Uat -p:RuntimeIdentifier=iossimulator-arm64 -p:EnableCodeSigning=false
```

Android local test signing and an unsigned simulator build do not establish distribution signing, TestFlight acceptance or live UAT authentication. Never install the owned local fixture as a UAT artifact. iOS UI cannot be driven in this environment. Exact local/emulator evidence is in VALIDATION.md.

The emulator appearance harness is deliberately separate from portable checks: build Development Debug with `-p:SwoovrOwnedApiTest=true` before starting the owned runner; use only the fictional emulator with explicit adb reverse5400/8180. Select `tests/mobile/appearance.android.cjs` with `SWOOVR_NATIVE_UI=1` and `SWOOVR_MEETING_API_PORT=5400`. It creates a private temporary fictional credential fixture, waits at most eight minutes for independent PostgreSQL persistence, and requires separately recorded logout/guest-restoration observation. Its finally block removes the credential file and the parent runner restores MAIN hosts. Do not rebuild while the runner is active.

Actual emulator acceptance also exercises registered-device removal on logout. That endpoint returns204 with no body; MemberClient returns its HTTP status directly rather than attempting JSON deserialization. A fake regression covers204 and401/503 refusals, and the emulator must prove logout guest restoration separately.
