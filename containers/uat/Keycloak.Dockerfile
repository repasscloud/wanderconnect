# Build from the repository root. No development realms, credentials or data.
# D255's internal provider ABI must stay at exactly Keycloak 26.8.1 / Java 21.
FROM quay.io/keycloak/keycloak:26.8.1@sha256:79c623a22abacf3fe6c00a5fb30f5b7843d717b234033b80995891c06116d244 AS keycloak-base

# Immutable official Eclipse Temurin Java 21 JDK index (amd64 and arm64).
FROM eclipse-temurin:21-jdk-noble@sha256:bd6ac06e2a2df928af21c75c4a8a0f2829a93377a65761d6a577bb32281e2ac3 AS provider-build
WORKDIR /source
COPY --from=keycloak-base /opt/keycloak/lib/lib/main/ /keycloak-libs/
COPY containers/keycloak/contact-policy/src/ ./provider/
COPY src/Swoovr.Contracts/ApiContract.cs ./ApiContract.cs
# Mirror check_keycloak_contact_policy.py's compilation, resources, generated
# protocol and policy checks. Build output contains main classes/resources only.
RUN set -eu; \
    test -f /keycloak-libs/org.keycloak.keycloak-server-spi-26.8.1.jar; \
    mkdir -p /build/main /build/tests /output; \
    javac --release 21 -classpath '/keycloak-libs/*' -d /build/main $(find provider/main/java -name '*.java' | sort); \
    cp -R provider/main/resources/. /build/main/; \
    sed -n 's/.*public const string Header = "\([^";]*\)";.*/header=\1/p; s/.*public const string Initial = "\([^";]*\)";.*/initial=\1/p' ApiContract.cs > /build/main/swoovr-api-contract.properties; \
    test "$(wc -l < /build/main/swoovr-api-contract.properties)" -eq 2; \
    javac --release 21 -classpath '/build/main:/keycloak-libs/*' -d /build/tests $(find provider/test/java -name '*.java' | sort); \
    java -classpath '/build/tests:/build/main:/keycloak-libs/*' app.swoovr.identity.RetirementPolicyChecks; \
    java -classpath '/build/tests:/build/main:/keycloak-libs/*' app.swoovr.identity.MailerSendChecks; \
    jar --create --file /output/swoovr-contact-policy.jar -C /build/main .

FROM keycloak-base AS keycloak-build
ENV KC_DB=postgres \
    KC_HEALTH_ENABLED=true \
    KC_METRICS_ENABLED=true
WORKDIR /opt/keycloak
COPY --from=provider-build --chown=1000:0 /output/swoovr-contact-policy.jar /opt/keycloak/providers/swoovr-contact-policy.jar
COPY --chown=1000:0 containers/keycloak/themes/swoovr/ /opt/keycloak/themes/swoovr/
# Keycloak checks provider mtimes at optimized startup; normalize before build.
RUN sed -i -e 's|^swoovrPrivacyUrl=.*|swoovrPrivacyUrl=https://uat.swoovr.app/privacy|' \
    -e 's|^swoovrTermsUrl=.*|swoovrTermsUrl=https://uat.swoovr.app/terms|' \
    -e 's|^swoovrCommunityUrl=.*|swoovrCommunityUrl=https://uat.swoovr.app/community-guidelines|' \
    -e 's|^swoovrVersion=.*|swoovrVersion=v0.1.0-uat|' /opt/keycloak/themes/swoovr/login/theme.properties \
    && touch -m --date=@1743465600 /opt/keycloak/providers/swoovr-contact-policy.jar \
    && /opt/keycloak/bin/kc.sh build --spi-email-sender--provider=swoovr-mailersend

FROM keycloak-base AS keycloak
ENV KC_DB=postgres \
    KC_HEALTH_ENABLED=true \
    KC_METRICS_ENABLED=true
COPY --from=keycloak-build --chown=1000:0 /opt/keycloak/ /opt/keycloak/
WORKDIR /opt/keycloak
USER 1000
EXPOSE 8080 8443 9000
COPY --chown=1000:0 --chmod=0555 containers/uat/keycloak-entrypoint.sh /opt/keycloak/bin/swoovr-entrypoint.sh
ENTRYPOINT ["/opt/keycloak/bin/swoovr-entrypoint.sh"]
CMD ["start", "--optimized"]
