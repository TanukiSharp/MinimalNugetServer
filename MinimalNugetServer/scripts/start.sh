#TZ=?/?
#export TZ
today=$(date +"%Y-%m-%d")
now=$(date +"%H.%M.%S")
mkdir -p logs
mkdir -p logs/${today}
dotnet MinimalNugetServer.dll "$@" >> "./logs/${today}/log_${now}.txt" 2>&1 &
echo $! > run.pid
