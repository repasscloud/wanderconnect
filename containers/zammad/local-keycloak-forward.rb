# Local only. Runs inside the Zammad Rails container's network namespace and forwards its loopback port 8180 to the local
# Keycloak (the platform's issuer is http://localhost:8180, which inside the container would otherwise be nothing).
# Zammad 7.2.2's OpenID Connect discovery always uses HTTPS, so a connection that starts with a TLS handshake is
# terminated here with a throwaway self-signed "localhost" certificate (trusted only by the Rails container); plain HTTP
# (token and key requests, at the discovered http:// endpoints) passes straight through. A hosted Keycloak is HTTPS and
# needs none of this.
require 'socket'
require 'openssl'

tls = OpenSSL::SSL::SSLContext.new
tls.cert = OpenSSL::X509::Certificate.new(File.read('/swoovr-local-tls/cert.pem'))
tls.key = OpenSSL::PKey.read(File.read('/swoovr-local-tls/key.pem'))
tls.min_version = OpenSSL::SSL::TLS1_2_VERSION
server = TCPServer.new('127.0.0.1', 8180)

def pump(from, to)
  loop { to.write(from.readpartial(65_536)) }
rescue StandardError
  nil
end

loop do
  raw = server.accept
  Thread.new(raw) do |socket|
    client = upstream = nil
    begin
      first = socket.recv(1, Socket::MSG_PEEK)
      client = if first == "\x16"
                 OpenSSL::SSL::SSLSocket.new(socket, tls).tap { |s| s.sync_close = true; s.accept }
               else
                 socket
               end
      upstream = TCPSocket.new('swoovr-platform', 8180)
      [Thread.new { pump(client, upstream); upstream.close_write rescue nil },
       Thread.new { pump(upstream, client); client.close rescue nil }].each(&:join)
    rescue StandardError
      nil
    ensure
      client&.close rescue nil
      socket.close rescue nil
      upstream&.close rescue nil
    end
  end
end
