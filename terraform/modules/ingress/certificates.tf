#------------------------------------------------------------------------------
# Certificate Authority
#------------------------------------------------------------------------------
resource "tls_private_key" "root_ingress" {
  count       = var.tls ? 1 : 0
  algorithm   = "RSA"
  ecdsa_curve = "P384"
  rsa_bits    = "4096"
}

resource "tls_self_signed_cert" "root_ingress" {
  count                 = length(tls_private_key.root_ingress)
  private_key_pem       = tls_private_key.root_ingress.0.private_key_pem
  is_ca_certificate     = true
  validity_period_hours = "168"
  allowed_uses = [
    "cert_signing",
    "key_encipherment",
    "digital_signature"
  ]
  subject {
    organization = "ArmoniK Ingress Root (NonTrusted)"
    common_name  = "ArmoniK Ingress Root (NonTrusted) Private Certificate Authority"
    country      = "France"
  }
}

#------------------------------------------------------------------------------
# Client Certificate Authority
#------------------------------------------------------------------------------
resource "tls_private_key" "client_root_ingress" {
  count       = var.mtls ? 1 : 0
  algorithm   = "RSA"
  ecdsa_curve = "P384"
  rsa_bits    = "4096"
}

resource "tls_self_signed_cert" "client_root_ingress" {
  count                 = length(tls_private_key.client_root_ingress)
  private_key_pem       = tls_private_key.client_root_ingress.0.private_key_pem
  is_ca_certificate     = true
  validity_period_hours = "168"
  allowed_uses = [
    "cert_signing",
    "key_encipherment",
    "digital_signature"
  ]
  subject {
    organization = "ArmoniK Client Ingress Root (NonTrusted)"
    common_name  = "ArmoniK Client Ingress Root (NonTrusted) Private Certificate Authority"
    country      = "France"
  }
}

#------------------------------------------------------------------------------
# Server Certificate
#------------------------------------------------------------------------------
resource "tls_private_key" "ingress_private_key" {
  count       = length(tls_private_key.root_ingress)
  algorithm   = "RSA"
  ecdsa_curve = "P384"
  rsa_bits    = "4096"
}

resource "tls_cert_request" "ingress_cert_request" {
  count           = length(tls_private_key.ingress_private_key)
  private_key_pem = tls_private_key.ingress_private_key.0.private_key_pem
  subject {
    country     = "France"
    common_name = "localhost"
  }
}

resource "tls_locally_signed_cert" "ingress_certificate" {
  count                 = length(tls_cert_request.ingress_cert_request)
  cert_request_pem      = tls_cert_request.ingress_cert_request.0.cert_request_pem
  ca_private_key_pem    = tls_private_key.root_ingress.0.private_key_pem
  ca_cert_pem           = tls_self_signed_cert.root_ingress.0.cert_pem
  validity_period_hours = "168"
  allowed_uses = [
    "key_encipherment",
    "digital_signature",
    "server_auth",
    "client_auth",
    "any_extended",
  ]
}

#------------------------------------------------------------------------------
# Client Certificate
#------------------------------------------------------------------------------
resource "tls_private_key" "ingress_client_private_key" {
  count       = length(tls_private_key.client_root_ingress)
  algorithm   = "RSA"
  ecdsa_curve = "P384"
  rsa_bits    = "4096"
}

resource "tls_cert_request" "ingress_client_cert_request" {
  count           = length(tls_private_key.ingress_client_private_key)
  private_key_pem = tls_private_key.ingress_client_private_key.0.private_key_pem
  subject {
    country     = "France"
    common_name = "client"
    # organization = "127.0.0.1"
  }
}

resource "tls_locally_signed_cert" "ingress_client_certificate" {
  count                 = length(tls_cert_request.ingress_client_cert_request)
  cert_request_pem      = tls_cert_request.ingress_client_cert_request.0.cert_request_pem
  ca_private_key_pem    = tls_private_key.client_root_ingress.0.private_key_pem
  ca_cert_pem           = tls_self_signed_cert.client_root_ingress.0.cert_pem
  validity_period_hours = "168"
  allowed_uses = [
    "key_encipherment",
    "digital_signature",
    "server_auth",
    "client_auth",
    "any_extended",
  ]
}

resource "pkcs12_from_pem" "ingress_client_pkcs12" {
  count           = length(tls_locally_signed_cert.ingress_client_certificate)
  password        = ""
  cert_pem        = tls_locally_signed_cert.ingress_client_certificate.0.cert_pem
  private_key_pem = tls_private_key.ingress_client_private_key.0.private_key_pem
  ca_pem          = tls_self_signed_cert.client_root_ingress.0.cert_pem
}

resource "local_sensitive_file" "ingress_ca" {
  count           = length(tls_self_signed_cert.root_ingress)
  content         = tls_self_signed_cert.root_ingress.0.cert_pem
  filename        = "${path.root}/generated/${var.container.name}/server/ca.crt"
  file_permission = "0644"
}

resource "local_sensitive_file" "ingress_client_crt" {
  count           = length(tls_locally_signed_cert.ingress_client_certificate)
  content         = tls_locally_signed_cert.ingress_client_certificate.0.cert_pem
  filename        = "${path.root}/generated/${var.container.name}/client/client.crt"
  file_permission = "0600"
}

resource "local_sensitive_file" "ingress_client_key" {
  count           = length(tls_private_key.ingress_client_private_key)
  content         = tls_private_key.ingress_client_private_key.0.private_key_pem
  filename        = "${path.root}/generated/${var.container.name}/client/client.key"
  file_permission = "0600"
}

resource "local_sensitive_file" "ingress_client_p12" {
  count           = length(pkcs12_from_pem.ingress_client_pkcs12)
  content_base64  = pkcs12_from_pem.ingress_client_pkcs12.0.result
  filename        = "${path.root}/generated/${var.container.name}/client/client.p12"
  file_permission = "0600"
}
