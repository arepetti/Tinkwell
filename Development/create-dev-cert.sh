#!/bin/bash

echo -e "\033[1;34mThis script will create a Tinkwell development certificate for local HTTPS using your computer's hostname.\033[0m"

read -s -p "Enter a password to protect the PFX file: " cert_pass
echo
read -p "Enter output folder or file path for the certificate: " export_path

hostname=$(hostname)

if [ -d "$export_path" ]; then
  cert_file="$export_path/$hostname-devcert.pfx"
else
  cert_file="$export_path"
fi

openssl req -x509 -nodes -days 1825 -newkey rsa:2048 \
  -keyout devkey.key \
  -out devcert.crt \
  -subj "/CN=$hostname" \
  -addext "subjectAltName=DNS:$hostname,DNS:localhost"

openssl pkcs12 -export -out "$cert_file" -inkey devkey.key -in devcert.crt -password pass:"$cert_pass"

rm devkey.key devcert.crt

echo -e "\033[32mCertificate successfully created at:\033[0m"
echo -e "\033[36m$cert_file\033[0m"