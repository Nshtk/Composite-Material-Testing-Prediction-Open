#!/bin/bash

if [[ ${1} == "-help" || ${1} == "--help" ]]; then
	echo "Usage: build.sh [ARCHITECTURE]
Example: ./run.sh linux-x64
Information: see dotnet publish specifications for list of available architectures." 
	exit 0
elif [[ -z "${1// }" ]]; then
	echo "Provide an architecture to run this project. Use --help for more info."
	exit 1
fi

PROJECT_NAME="CMTP.Desktop"

./Source/Artifacts/${PROJECT_NAME}/bin/Release/net8.0/${1}/publish/${PROJECT_NAME}