# Reported
A meme discord bot to have fun with your friends

### Installation

In order to install this bot as of right now, it expects you some engineering knowledge.

#### Prerequisites:

* dotnet SDK, the latest [can be installed here](https://dotnet.microsoft.com/en-us/download)
* An application in [discord developer](https://discord.com/developers)
* An axiom dataset setup for ingesting logs https://axiom.co/ (this can be removed by commenting out relevant code if you rather just rely on syslogs)
* A linux environment to deploy to

#### Publish:
All you need to do is run the following command:
```bash
dotnet publish
```
The output will be where the app has been published to. Copy this to the location you want to run it from. I suggest a small VPS or use your own dedicated hardware for example, a RaspberryPi

##### Service File
For your convenience, there is a systemd service file (`./Reported/Reported.service`) that you can use to run your this application. Update it with your specific deployment configuration and run it. Running and enabling systemd service files is out of scope for this documentation (for now).

#### Inspiration

This bot was inspired by a group of my friends always saying "reported" when we do something of note. It's always said in jest. One of my friends mentioned that we should turn it into a bot to make our words into "reality". 

#### Shoutouts
Thank you AAQ for your help with testing and thinking up these dumb ideas