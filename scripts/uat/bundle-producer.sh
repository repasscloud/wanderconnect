#!/bin/sh
set -eu
cd /app
exec dotnet /app/Swoovr.Api.dll backups bundle
