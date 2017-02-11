CONFIGURATION=release
RUNTIME=netcoreapp1.1
OUTPUT=../bin/${CONFIGURATION}/${RUNTIME}/publish/
pushd ..
dotnet restore
dotnet publish -c ${CONFIGURATION}
popd
cp start.sh ${OUTPUT}
cp stahp.sh ${OUTPUT}
cp ../configuration.json ${OUTPUT}
chmod +x ${OUTPUT}start.sh
chmod +x ${OUTPUT}stahp.sh
