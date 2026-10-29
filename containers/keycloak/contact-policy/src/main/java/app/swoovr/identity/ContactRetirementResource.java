package app.swoovr.identity;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import jakarta.persistence.EntityManager;
import jakarta.ws.rs.Consumes;
import jakarta.ws.rs.GET;
import jakarta.ws.rs.POST;
import jakarta.ws.rs.Path;
import jakarta.ws.rs.PathParam;
import jakarta.ws.rs.Produces;
import jakarta.ws.rs.core.MediaType;
import jakarta.ws.rs.core.Response;
import jakarta.ws.rs.ext.Provider;
import org.keycloak.common.util.Time;
import org.keycloak.connections.jpa.JpaConnectionProvider;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.RealmModel;
import org.keycloak.models.cache.UserCache;
import org.keycloak.services.managers.AppAuthManager;
import org.keycloak.services.managers.AuthenticationManager;
import org.keycloak.services.resources.admin.AdminAuth;
import org.keycloak.storage.UserStoragePrivateUtil;

/** New custom provider endpoint, not a vanilla Keycloak API. Blocking operations share Keycloak's request transaction. */
@Provider
@Produces(MediaType.APPLICATION_JSON)
@Consumes(MediaType.APPLICATION_JSON)
public final class ContactRetirementResource {
    private final KeycloakSession session;
    public ContactRetirementResource(KeycloakSession session) { this.session=session; }
    public record RetirementRequest(String operationId,String expectedEmail,String expectedUsername) { }

    private RealmModel authorize() {
        ProviderProtocol.select(session.getContext().getHttpRequest().getHttpHeaders());
        var realm=session.getContext().getRealm();
        if(!"enabled".equals(realm.getAttribute("swoovr.contact-policy")))throw ProviderProtocol.refusal(404,"contact-policy-not-enabled");
        var auth=new AppAuthManager.BearerTokenAuthenticator(session).setRealm(realm).authenticate();
        if(auth==null)throw ProviderProtocol.refusal(401,"authentication-required");
        var client=auth.client();var user=auth.user();
        var required=realm.getAttribute("swoovr.contact-policy.client");if(required==null)required="swoovr-api-accounts";
        if(client==null||!client.isEnabled()||!client.isServiceAccountsEnabled()||!required.equals(client.getClientId())
            ||user==null||!Objects.equals(user.getServiceAccountClientLink(),client.getId()))throw ProviderProtocol.refusal(403,"service-account-required");
        var management=session.clients().getClientByClientId(realm,"realm-management");
        if(management==null||!new AdminAuth(realm,auth.token(),user,client).hasAppRole(management,"manage-users"))
            throw ProviderProtocol.refusal(403,"manage-users-required");
        return realm;
    }
    private EntityManager database() { return session.getProvider(JpaConnectionProvider.class).getEntityManager(); }
    private static UUID identity(String value) {
        try { return UUID.fromString(value); } catch(IllegalArgumentException|NullPointerException error) { throw ProviderProtocol.refusal(400,"invalid-identity"); }
    }
    private static Map<String,Object> completed(RetiredContact marker) {
        return Map.of("operationId",marker.operationId,"userId",marker.userId,"status","retired");
    }
    private void requireGuard() {
        var present=database().createNativeQuery("SELECT to_regclass('swoovr_email_retirement') IS NOT NULL AND EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid=to_regclass('user_entity') AND tgfoid=to_regprocedure('swoovr_retired_contact_guard()') AND tgname='swoovr_retired_contact_guard' AND tgenabled IN ('O','A') AND NOT tgisinternal)",Boolean.class).getSingleResult();
        if(!Boolean.TRUE.equals(present))throw ProviderProtocol.refusal(503,"contact-policy-unavailable");
    }
    @GET @Path("capabilities")
    public Response capabilities() {
        authorize();requireGuard();
        return ProviderProtocol.reply(200,Map.of("keycloak",ContactEntityProviderFactory.KEYCLOAK_VERSION,"retireEmail",true,"schema",1));
    }
    public record EmailBinding(String userId,String email,String username,boolean emailVerified,boolean enabled) { }
    @GET @Path("users/{userId}/email-binding")
    @SuppressWarnings("unchecked")
    public Response binding(@PathParam("userId")String userValue) {
        var realm=authorize();requireGuard();var user=identity(userValue);
        List<Object[]> rows=database().createNativeQuery("SELECT email,username,federation_link,service_account_client_link,realm_id,email_verified,enabled FROM user_entity WHERE id=:id AND realm_id=:realm")
            .setParameter("id",user.toString()).setParameter("realm",realm.getId()).getResultList();
        if(rows.isEmpty())throw ProviderProtocol.refusal(404,"source-not-found");
        var raw=rows.getFirst();
        if(raw[2]!=null||raw[3]!=null)throw ProviderProtocol.refusal(409,"unsupported-source");
        return ProviderProtocol.reply(200,new EmailBinding(user.toString(),(String)raw[0],(String)raw[1],Boolean.TRUE.equals(raw[5]),Boolean.TRUE.equals(raw[6])));
    }
    @GET @Path("users/{userId}/retire-email/{operationId}")
    public Response status(@PathParam("userId")String userValue,@PathParam("operationId")String operationValue) {
        var realm=authorize();requireGuard();var user=identity(userValue);var operation=identity(operationValue);
        var marker=database().find(RetiredContact.class,user.toString());
        if(marker==null||!marker.realmId.equals(realm.getId())||!marker.operationId.equals(operation.toString()))throw ProviderProtocol.refusal(404,"retirement-not-found");
        var matching=database().createNativeQuery("SELECT count(*) FROM user_entity WHERE id=:id AND realm_id=:realm AND email=:sink AND email_verified=false AND (:login=false OR username=:sink)",Long.class)
            .setParameter("id",marker.userId).setParameter("realm",marker.realmId).setParameter("sink",marker.sinkEmail).setParameter("login",marker.loginRetired).getSingleResult();
        if(!Long.valueOf(1).equals(matching))throw ProviderProtocol.refusal(503,"retirement-state-unavailable");
        return ProviderProtocol.reply(200,completed(marker));
    }
    @POST @Path("users/{userId}/retire-email")
    @SuppressWarnings("unchecked")
    public Response retire(@PathParam("userId")String userValue,RetirementRequest request) {
        var realm=authorize();requireGuard();var userId=identity(userValue);
        if(request==null)throw ProviderProtocol.refusal(400,"invalid-request");
        var operation=identity(request.operationId());
        if(request.expectedEmail()==null||request.expectedEmail().isBlank()||request.expectedEmail().length()>254
            ||request.expectedUsername()==null||request.expectedUsername().isBlank()||request.expectedUsername().length()>255)
            throw ProviderProtocol.refusal(400,"expected-binding-required");
        var em=database();
        // Lock before reading the current binding; API account locks alone cannot serialize provider-owned writers.
        List<Object[]> rows=em.createNativeQuery("SELECT email,username,federation_link,service_account_client_link,realm_id,email_verified FROM user_entity WHERE id=:id AND realm_id=:realm FOR UPDATE")
            .setParameter("id",userId.toString()).setParameter("realm",realm.getId()).getResultList();
        if(rows.isEmpty()||!realm.getId().equals(rows.getFirst()[4]))throw ProviderProtocol.refusal(404,"source-not-found");
        var raw=rows.getFirst();
        var marker=em.find(RetiredContact.class,userId.toString());
        if(marker!=null) {
            if(!marker.realmId.equals(realm.getId())||!marker.operationId.equals(operation.toString()))throw ProviderProtocol.refusal(409,"another-retirement-recorded");
            if(!marker.sinkEmail.equals(raw[0])||(marker.loginRetired&&!marker.sinkEmail.equals(raw[1]))||!Boolean.FALSE.equals(raw[5]))
                throw ProviderProtocol.refusal(503,"retirement-state-unavailable");
            return ProviderProtocol.reply(200,completed(marker));
        }
        if(raw[2]!=null||raw[3]!=null)throw ProviderProtocol.refusal(409,"unsupported-source");
        if(!RetirementPolicy.unchanged((String)raw[0],(String)raw[1],request.expectedEmail(),request.expectedUsername()))
            throw ProviderProtocol.refusal(409,"binding-changed");
        var user=UserStoragePrivateUtil.userLocalStorage(session).getUserById(realm,userId.toString());
        if(user==null)throw ProviderProtocol.refusal(404,"source-not-found");
        marker=new RetiredContact();marker.userId=userId.toString();marker.realmId=realm.getId();marker.operationId=operation.toString();
        marker.sinkEmail=RetirementPolicy.sink(operation);marker.loginRetired=RetirementPolicy.sameContact((String)raw[1],(String)raw[0]);marker.retiredAt=Instant.ofEpochMilli(Time.currentTimeMillis());
        em.persist(marker);
        user.setEmail(marker.sinkEmail);user.setEmailVerified(false);
        if(marker.loginRetired)user.setUsername(marker.sinkEmail);
        session.users().setNotBeforeForUser(realm,user,Math.toIntExact(Time.currentTimeMillis()/1000L));
        session.sessions().getUserSessionsStream(realm,user).toList().forEach(active->AuthenticationManager.backchannelLogout(session,realm,active,
            session.getContext().getUri(),session.getContext().getConnection(),session.getContext().getHttpRequest().getHttpHeaders(),true));
        em.flush();
        var cache=session.getProvider(UserCache.class);if(cache!=null)cache.evict(realm,user);
        return ProviderProtocol.reply(200,completed(marker));
    }
}
