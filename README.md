# EngryptedMirror
Open an encrypted folder as a drive in Windows

To use the program, start it specifying the arguments: "letter folder"

Ex: 

MagicMirror.exe H c:/encrypted_folder

This will ask you for a password in a console, as long as the console is up and running the letter "H:" will be available in the explorer ( and any other program ).
Anything put inside it will be encrypted and put inside the path provided ( c:/encrypted_folder in the example )

*warning*
If the wrong password is provided, you'll be able to add files but they will be encrypted with the new (wrong) password.
There is no validation done, you just won't be able to open existing files with the wrong password. You'll either get generic errors from the explorer or the software you're using.
