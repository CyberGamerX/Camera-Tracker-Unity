# Camera-Tracker-Unity
Unity reciever for the parallel project Camera Tracker Arduino.

This project recieves custom UDP packets from the parallel arduino project representing a cameras rotation and position. The project attempts to recreate and sync the virtual cameras position to the recieved data.

The project is intended to interface with a Canon Camera via the EDSDK.dll. Due to licencing, the EDSDK is not included in the repository and must be added seperatly to run the project. In addition, a usb connected Canon Camera is required for the project to start properly. Safetys are not present at this time.

## Base Project
[Camera Tracker Arduino](https://github.com/CyberGamerX/Camera-Tracker-Arduino)