set CONFIGURATION=release
set RUNTIME=netcoreapp1.1
set OUTPUT=..\bin\%CONFIGURATION%\%RUNTIME%\publish\

pushd ..
dotnet restore
dotnet publish -c %CONFIGURATION%
popd

copy start.sh %OUTPUT%
copy stahp.sh %OUTPUT%
copy ..\configuration.json %OUTPUT%

pause
