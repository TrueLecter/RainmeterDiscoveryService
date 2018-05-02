#!/bin/bash
requiredver='8.0.0'
currentver=`node -v`
scriptPath="/usr/share/discovery-service.js"
serviceFileName="discovery-client"
serviceFilePath="/lib/systemd/system/$serviceFileName.service"

command -v node >/dev/null 2>&1 || { echo >&2 "NodeJS was not found\nInstall it using https://nodejs.org/en/download/package-manager/\nExiting..."; exit 1; }

nodePath=`command -v node`

command -v curl >/dev/null 2>&1 || { echo >&2 "curl was not found\nExiting..."; exit 1; }

if [ `printf '%s\n' "$requiredver" "$currentver" | sort -V | head -n1` = "$currentver" ]; then 
    echo >&2 "NodeJS fround but version $currentver lower than required $requiredver \nExiting..."
    exit 2
fi

if ! [ $(id -u) = 0 ]; then
    echo "Script was not run with superuser permissions"
    exit 3
fi

echo "Installing..."

echo "Downloading script to '$scriptPath'..."
curl https://raw.githubusercontent.com/TrueLecter/RainmeterDiscoveryService/master/discovery-client.js 2>/dev/null > $scriptPath
echo "Downloaded."

echo "Creating service..."
echo "
[Unit]
Description=Discovery client
After=network.target

[Service]
Type=simple
ExecStart=$nodePath $scriptPath
Restart=on-failure

[Install]
WantedBy=multi-user.target
" > $serviceFilePath
echo "Service file created. Reloading systemctl daemon..."
systemctl daemon-reload
echo "Starting service $serviceFileName..."
service $serviceFileName start
echo "Done."
