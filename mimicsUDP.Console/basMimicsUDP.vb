
Module basMimicsUDP

    Sub Main()

        Try
            Dim cls As New mimicsUDP.DLL.clsMimicsUDP
            cls.Main("console") '
        Catch ex As Exception
            MsgBox(ex.Message)
        End Try

    End Sub
    
  End Module
