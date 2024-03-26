set base=..\Bin
set src=%base%\Server

set port=1883
set cport=2001
set dst=%base%\Cluster%cport%
xcopy %src%\*.exe %dst%\ /y
xcopy %src%\*.dll %dst%\ /y
xcopy %src%\*.json %dst%\ /y
start %dst%\MqttServer.exe -port %port% -clusterPort %cport% -clusterNodes 127.0.0.3:2001,127.0.0.3:2002,127.0.0.3:2003,

set port=2883
set cport=2002
set dst=%base%\Cluster%cport%
xcopy %src%\*.exe %dst%\ /y
xcopy %src%\*.dll %dst%\ /y
xcopy %src%\*.json %dst%\ /y
start %dst%\MqttServer.exe -port %port% -clusterPort %cport% -clusterNodes 127.0.0.3:2001,127.0.0.3:2002,127.0.0.3:2003,

set port=3883
set cport=2003
set dst=%base%\Cluster%cport%
xcopy %src%\*.exe %dst%\ /y
xcopy %src%\*.dll %dst%\ /y
xcopy %src%\*.json %dst%\ /y
start %dst%\MqttServer.exe -port %port% -clusterPort %cport% -clusterNodes 127.0.0.3:2001,127.0.0.3:2002,127.0.0.3:2003,
