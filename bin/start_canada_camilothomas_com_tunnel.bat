taskkill /f /im plink.exe
:loop
C:\Users\camilo\code\operating_system\tool\bin\plink.exe -load "canada.camilothomas.com" -N -T
echo Program has closed. Restarting...
timeout /t 1 /nobreak >nul
goto loop
