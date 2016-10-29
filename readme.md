# Overview

This is a minimal implementation of a NuGet server.

It supports API v2 for most of usages with Visual Studio package manager (search packages, find package versions, etc...) and it also support v3 with `dotnet restore` only, not Visual Studio support for v3 APIs.

It basically works the same as if you were using a file system folder as package source.

This server provides us a standardized way to access our internally developped packages.

# Note before getting started

Since it is developped for .NET Core runtime, it is possible to build and run it virtually everywhere .NET Core runs.

However, all the following instructions are written considering a Windows and Linux workstation to build and generate a publishable, and Linux machine as an hosting server.

Also, please understand that this is a very simple and naive implementation, that could be suited for individuals or small organizations.

See the section `Future works` at the bottom of this document for more details.

# Problems it solves

We need to self-host our internally developped packages, and prior this implementation, we were using a samba shared folder, hosted on a Linux machine.

The main problems were:
- Some operating systems have hard time to connect to a samba shared folder
- Sometimes (and for unknown reasons) if a package source is unavailable, `dotnet restore` fails instead of skipping it
- Different machines need different `nuget.config` configurations

Using the HTTP interface over samba shared folder one is a no brainer.

It seems for the moment the only available implementations of NuGet server are made for Windows only, but we need to host one on a Linux machine, so .NET Core is the perfect candidate runtime for that.

# How to use it

## Build

In a terminal, go to the directory that contains the project.json and run the `dotnet build` command.

When you build for the first time, you may get the following error:

    error CS0103: The name 'GitCommitHash' does not exist in the current context

This is because a file is generated before compilation, and is not taken into account for the build.
Just run `dotnet build` again and the it will finally compile correctly.

## Deploy

### For the impatients

    cd MinimalNugetServer/scripts

    # === on Windows ===
    #make_publishable.bat

    # === on Linux ===
    #chmod +x make_publishable.sh && ./make_publishable.sh

    cd ../bin/Release/netcoreapp1.0/publish/
    # ===== edit the configuration.json file
    dotnet run

### Detailed explanations

For all `.sh` scripts you will need to run on Linux, remember that they must first be made executable by running the command `chmod +x <script>`.

You can use the `dotnet run` command to run it directly from the directory that contain the `project.json` file, but you would first have to modify the default `configuration.json` file provided.

In the `scripts` directory, you can run the script `make_publishable`, either the `.bat` for Windows or the `.sh` for Linux (never tested on Mac).

This will make everything ready in the directory `bin/Release/netcoreapp1.0/publish` for you to deploy to a hosting server.

Once deployed where you want to run it, execute the command `./start.sh`.

The `start.sh` script runs the server detached from the terminal that ran it, redirects all outputs to a log file, and creates a `run.pid` file to track the process and eventually stop it more easily if needed.

To check the log, run the command `tail -f logs/<date>/log_<time>.txt` file.
This will display the last few lines of the log file, and also prints the new comming log entries (watch mode).

## Configuration

The configuration file is pretty much straightforward and self explanatory.

```
{
  "server": {
    "url": "http://*:4356"
  },
  "nuget": {
    "packages": "/absolute/path/to/packages/folder"
  }
}
```

The `server` section holds the configuration related to the hosting server, and for now it contains only the `url` key, which is where the server will listen for requests.
You may not need to change this unless you want to change the port. The port has been chosen purely randomly, we call this very advanced technique *to throw a shoe on the keyboard*.

The `nuget` section holds the configuration related to the NuGet things, and for now it contains only the `packages` key, which is the directory where your packages are stored.

# Runtime

On the client side, you have to add the following package source to your `nuget.config` file, either manually or through Visual Studio:
- http://*\<host\>*:4356/v2 to use the v2 API (good support)
- http://*\<host\>*:4356/v3 to use the v3 API (`dotnet restore` only)

From the server point of view, it does not support publishing APIs, as a shared folder would. To add new packages, you have to copy them manually to the packages directory on your hosting machine.

The server uses a `FileSystemWatcher` to detect new files, files removed and files renamed, and will reprocess the whole packages collection for future incomming requests.

# Future works

For now, the server loads all the packages (file content) in memory at start time, and each time the packages folder changes.

Improvements could be:
- Lazy loading of packages, with a MRU cache to avoid consuming too much memory (feasible)
- Reprocessing only the necessary items (a bit more tricky)
- Better support of the v3 API once documented by the NuGet team
