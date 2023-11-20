# Tracker

So, for those of us that are worried about theft, I thought I might make a little program I can install on my tablet, so it will update a public service (eg, perhaps a web service somewhere, send an email on regular interval with location, who knows) and if the kit ever disappears, it'll sing the moment it's turned on again and sees some AOG location data and for this, I thought TRACCAR would be ideal.

Although I'm using TRACCAR, you could change it to do anything you like - send an email, SMS, you name it.

TODO: Build or rent a TRACCAR server from traccar.org if you don't fancy doing one yourself.

Register a device in the TRACCAR admin panel and note the ID you choose for it. Could be the registration plate on your tractor, anything.

Download and compile this solution (I'll add a Releases when I get round to it, along with prettier instructions)


## How it works

The program is installed (it doesn't run from where you saved or compiled the file to) and a scheduled task to Run on Startup is created. A small INI file is saved with config details in the same location. That's pretty much it. Obviously, given this isn't supposed to be easy for thieves to find, you'll want to pick somewhere to bury it, deep in the filesystem.

Find the EXE your compilation produces (I'll sort Releases out soon), and from an elevated command prompt:

    C:\temp>.\tracker /?
    Tracker - help

    tracker /install <id> <path to tracker.exe> <scheduled task path> <baseurl>
    eg tracker /install yourid c:\windows\system32\altupdate.exe \Microsoft\Windows\Shell\AltUpdateTask mytracker.site.com

    tracker /uninstall <scheduled task path>

    To ensure all is well, run it interactively for a while first and check TRACCAR
    tracker /interactive



So, if your TRACCAR server is "mytraccar.farm.com" and you registered a device ID AUTOTRACK1

    c:\temp> tracker /install AUTOTRACK1 c:\windows\system32\updater.exe \Microsoft\Windows\Shell\Updater mytraccar.farm.com

That will copy the tracker executable to \windows\system32\updater.exe (for safety, it won't overwrite any existing file) and create a Scheduled Task nested in the \Microsoft\Windows\Shell section naming it "Updater".

__*KEEP A NOTE OF THAT INFORMATION!!!"*__

If you ever want to delete it, you will reference the scheduled task.

    c:\temp> tracker /uninstall \Microsoft\Windows\Shell\Updater

It will remove the scheduled task and dump the config of same out - you'll see the name of the Program you can manually remove yourself right at the bottom.

Bit of a sketchy setup, better instructions to come. Enjoy!

If you are having any issues and want to run it interactively to see what's going on, find the Scheduled Task and stop it, then do
    
    c:\temp> c:\windows\system32\updater.exe /interactive

Note that you can only run from the installed location, the one you just downloaded won't work as it doesn't have the config file next to it.