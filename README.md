# Overview
This is a possibly slightly overengineered program to solve the issue of Dungeons of Dredmor crashing randomly without any error message or log, taking your character out with it.  This program is very similar to Sharpevil's DredBakup, except mine is a bit more complex, and theirs is a lot more cool aesthetically.

# Installation
This is just one executable, but I recommend making a folder for it, and then another folder inside that folder for the backups.  The reason I recommend making a folder for the program itself is because it will create a log (.log) and a config (.json).  The log is simply to help keep track of things, in case you have an issue finding a backup or aren't sure if one was deleted.  The config is there so you don't have to set the directories and whatever other settings you like every time you start the program.

<img width="611" height="126" alt="image" src="https://github.com/user-attachments/assets/ea12ec07-e90d-4f57-900d-3cfb508d33b9" />

# Usage
The program has tooltips for everything, but here's a rundown.  "Source folder" is the folder to create backups of.  This should be your Dungeons of Dredmor folder or specific character save folder.  I prefer the whole folder, because it includes autosaves.  The Dungeons of Dredmor folder is in your user documents, and your character saves are inside of it.  "Destination" (I forgot to put the word "folder" in it) is the folder your backups will be put in.  As mentioned in the previous section, I recommend using a backup folder inside the folder you put the program in.  I also highly encourage using a destination folder that is completely empty and exclusively used for this program because it will get scared otherwise (I will explain what I mean in the next section).

<img width="546" height="473" alt="image" src="https://github.com/user-attachments/assets/bfd4bc2c-078f-4015-a2a0-f3766eeddda5" />

# Technical
Because the program automatically deletes its backups, it felt necessary to implement safeguards.  Firstly, the program checks if the files within the destination folder are .zip, and if they are, it goes to the next step of verification.  Next it checks if the file name contains "Backup_," and if it does, it goes to the next and final stage.  The final stage of verification is checking if the file's name is 26 characters long.  The program's timestamp is always 26 characters long, so this will always be true for its backups.  If the length is 26, it is allowed to be deleted.  There is another separate safeguard that checks how many files are being deleted.  If there are multiple files to be deleted, the program will ask if you're sure.  This is to prevent accidental deletion of backups in the event you mistakenly change the max backup setting (for example, changing it from the default 10 to 1 and losing a slightly older backup you needed).  As for what I meant about the program getting scared, if any of these safeguards are activated, it will say something in the log.  So, while it shouldn't delete anything important if it's put with other files, it will still spam the log and probably run a lot worse.

# Development
This is a C# WinForms program, but you may notice that the Form1.cs file is pretty much empty.  This is because I didn't know the WinForms designer even existed at first, and then I decided to just keep going anyway.  In other words, it's a little messy.

# License
GNU General Public License v3.0
