# VSRemoteDebugger
Remote SSH Debugging tool for Visual Studio 2019 (ARM/Raspberry Pi compatible)

## The extension performs the following steps:

1. Builds the solution in Visual Studio 
2. Copies the fiels from the output folder into the remote machine
3. Connects to the VsDbg server and starts debugging the current project via SSH

## Requirements

- SSH based authentification set up between local and remote.

	The private key on the local machine needs to be `~\.ssh\id_rsa`  
	The public key on the remote machine needs to be `~/.ssh/authorized_keys`

- WinSCP installed on the local machine