# VSRemoteDebugger
Remote SSH Debugging tool for Visual Studio 2019 (ARM/Raspberry Pi compatible)

## Usage guide

- SSH based authentification needs to be set up between local and remote.

	Set the private key on the local machine as `~\.ssh\id_rsa`  
	Set the public key on the remote machine as `~/.ssh/authorized_keys`

- Install this extension
- In Visual Studio go to `Tools -> Settings -> VsRemoteDebugger -> Remote Machine Settings` and modify the access settings
- In Visual Studio go to `Tools -> (click on) Start Remote Debugger`

## The extension performs the following steps:

1. Builds the solution in Visual Studio 
2. Copies the fiels from the output folder into the remote machine
3. Connects to the VsDbg server and starts debugging the current project via SSH

## Limitations

- Publishing only available for .NET Core projects
- Not tested (and most likely not working) for 32-bit versions of Windows

