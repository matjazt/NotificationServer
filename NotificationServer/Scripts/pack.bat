@SET SVCWATCHDOGFOLDER=..\..\..\SysTools\SvcWatchDog\SvcWatchDogDist
@SET OUTPUTFOLDER=..\dist

@echo Folder %OUTPUTFOLDER% will be deleted and recreated. If you don't want this, press ctrl-c.

pause

if exist %OUTPUTFOLDER% rd /s /q %OUTPUTFOLDER%

mkdir %OUTPUTFOLDER%\Bin
mkdir %OUTPUTFOLDER%\Service
mkdir %OUTPUTFOLDER%\Etc
mkdir %OUTPUTFOLDER%\Doc\SvcWatchDog

copy /y %SVCWATCHDOGFOLDER%\SvcWatchDog\SvcWatchDog.exe %OUTPUTFOLDER%\Service\NotificationServerService.exe
copy /y %SVCWATCHDOGFOLDER%\Doc\* %OUTPUTFOLDER%\Doc\SvcWatchDog

copy /y ..\bin\Release\net9.0\* %OUTPUTFOLDER%\Bin
copy /y ..\Etc\NotificationServerService.json %OUTPUTFOLDER%\Service
copy /y ..\Etc\NotificationServer.json %OUTPUTFOLDER%\Etc
copy /y ..\Etc\*.p12 %OUTPUTFOLDER%\Etc

pause