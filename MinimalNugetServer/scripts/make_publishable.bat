set CONFIGURATION=release
set RUNTIME=netcoreapp2.0
set OUTPUT=..\bin\%CONFIGURATION%\%RUNTIME%\publish\

pushd ..
dotnet publish -c %CONFIGURATION%
popd

copy start.sh %OUTPUT%
copy stahp.sh %OUTPUT%
copy ..\configuration.json %OUTPUT%

pause
