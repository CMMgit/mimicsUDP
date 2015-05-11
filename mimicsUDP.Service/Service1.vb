Imports System.IO

Public Class Service1
  
    Protected Overrides Sub OnStart(ByVal args() As String)
        
        Try
            ' Add code here to start your service. This method should set things
            ' in motion so your service can do its work.
            WriteToLog("Service started: " & Now.ToString("dd MMM yyyy HH:mm:ss"))

            Dim cls As New mimicsUDP.DLL.clsMimicsUDP
            cls.Main()

        Catch ex As Exception
            WriteToLog("Exception (OnStart): " & ex.Message)
        End Try
    End Sub

    Protected Overrides Sub OnStop()
        ' Add code here to perform any tear-down necessary to stop your service.
        WriteToLog("Service stopped: " & Now.ToString("dd MMM yyyy HH:mm:ss"))
    End Sub
    Private Sub WriteToLog(ByVal strMessage As String)

        Dim strLogFilename As String = "C:\CMM mimics\mimics.service.log"
        If Not File.Exists(strLogFilename) Then
            Dim s As New StreamWriter(strLogFilename)
            s.WriteLine("Created: " & Now.ToString("dd MMM yyyy HH:mm:ss"))
            s.WriteLine("Created by the mimicsUDP.Service - reads mimics data log stream to local MySQL")
            s.WriteLine("-------------------------------------------------------------------------------------")
            s.Close()
        End If

        Dim sw As New StreamWriter(strLogFilename, True)
        sw.WriteLine(strMessage)
        sw.Close()

    End Sub
    
End Class
