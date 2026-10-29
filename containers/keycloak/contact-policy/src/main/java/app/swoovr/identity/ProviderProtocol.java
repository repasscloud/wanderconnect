package app.swoovr.identity;

import java.io.IOException;
import java.time.LocalDate;
import java.time.format.DateTimeParseException;
import java.util.Map;
import java.util.Properties;
import jakarta.ws.rs.WebApplicationException;
import jakarta.ws.rs.core.HttpHeaders;
import jakarta.ws.rs.core.MediaType;
import jakarta.ws.rs.core.Response;

/** Build-generated from Swoovr.Contracts.ApiContract; no independent supported-date constant. */
public final class ProviderProtocol {
    private static final Properties contract=load();
    public static final String HEADER=contract.getProperty("header");
    public static final String INITIAL=contract.getProperty("initial");
    private ProviderProtocol() { }
    private static Properties load() {
        try(var stream=ProviderProtocol.class.getResourceAsStream("/swoovr-api-contract.properties")) {
            if(stream==null)throw new IllegalStateException("Generated Swoovr contract missing.");
            var properties=new Properties();properties.load(stream);
            if(properties.getProperty("header")==null||properties.getProperty("initial")==null)throw new IllegalStateException("Generated Swoovr contract invalid.");
            return properties;
        } catch(IOException error) { throw new IllegalStateException("Generated Swoovr contract unreadable.",error); }
    }
    public static void select(HttpHeaders headers) {
        if(headers.getHeaderString(HEADER)==null)return;
        var values=headers.getRequestHeader(HEADER);
        if(values==null||values.size()!=1)throw badVersion("invalid-api-version");
        String value=values.get(0);
        if(value==null||!value.matches("[0-9]{4}-[0-9]{2}-[0-9]{2}"))throw badVersion("invalid-api-version");
        try { if(LocalDate.parse(value).getYear()<1)throw badVersion("invalid-api-version"); } catch(DateTimeParseException error) { throw badVersion("invalid-api-version"); }
        if(!INITIAL.equals(value))throw badVersion("unsupported-api-version");
    }
    private static WebApplicationException badVersion(String code) {
        return new WebApplicationException(Response.status(400).type(MediaType.APPLICATION_JSON_TYPE).header("Vary",HEADER).header("Cache-Control","no-store").entity(Map.of("code",code)).build());
    }
    public static Response reply(int status,Object value) {
        return Response.status(status).type(MediaType.APPLICATION_JSON_TYPE).header(HEADER,INITIAL).header("Vary",HEADER).header("Cache-Control","no-store").entity(value).build();
    }
    public static WebApplicationException refusal(int status,String code) { return new WebApplicationException(reply(status,Map.of("code",code))); }
}
