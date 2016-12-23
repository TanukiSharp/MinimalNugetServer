set CONFIGURATION=Release
set OUTPUT=..\bin\%CONFIGURATION%\netcoreapp1.1\publish\

pushd ..
dotnet restore
dotnet publish -c %CONFIGURATION%
popd

copy start.sh %OUTPUT%
copy stahp.sh %OUTPUT%
copy ..\configuration.json %OUTPUT%

pause
