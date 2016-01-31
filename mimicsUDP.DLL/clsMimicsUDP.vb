Imports System
Imports MySql.Data
Imports MySql.Data.MySqlClient
Imports System.Collections.Generic
Imports System.Text
Imports System.Net.Sockets
Imports System.Net
Imports System.Threading
Imports System.Reflection
Imports System.IO
Imports Microsoft.VisualBasic


Public Class clsMimicsUDP

    Private MySqlCon As MySqlConnection
    Dim strSql, strSqlINSERTholding, strSqlUPDATEholding As String
    Private strVersion As String = "Version 1.22 29/01/2016"
    Private strSubnet As String
    Private blnFractions As Boolean = False
    Private strStep As String

    Public Sub Main(Optional ByVal ConsoleOrService As String = "service")

        Try
            WriteToLog("CMM mimics " & ConsoleOrService & " data logger started ....... log on next", False)
            Dim MysqlConnString As String = GetIni("MysqlConnString")
            '"Data Source=localhost;Database=CMM;User ID=root;Password=admin;" '
            MySqlCon = New MySqlConnection(MysqlConnString)
            MySqlCon.Open()
            WriteToLog("Logged on to MySql successfully", True)
            strSubnet = GetIni("Subnet")
            If (Microsoft.VisualBasic.Right(strSubnet, 1) <> ".") Then strSubnet = strSubnet & "."

            Dim strFractions As String = GetIni("Fractions")
            If strFractions.ToUpper = "FALSE" Then blnFractions = False
            If strFractions.ToUpper = "TRUE" Then blnFractions = True

            Dim asmName As AssemblyName = Assembly.GetExecutingAssembly().GetName()
            Console.WriteLine("CMM mimics, Copyright 2014")
            Console.WriteLine(strVersion)
            Console.WriteLine("Subnet : " & strSubnet & "x")

            'Console.WriteLine("{0} Version {1}", asmName.Name, asmName.Version.ToString())

            Console.WriteLine("")

            'Dim ipList As IPAddress() = Dns.GetHostEntry("").AddressList

            '' find my IPv4 address; remember the computer can support many IP address depending
            '' the network card, WiFi used. We want an IPv4 local IP address
            'For Each ip As IPAddress In ipList
            '    If ip.AddressFamily = AddressFamily.InterNetwork Then
            '        ipHost = ip
            '        Exit For
            '    End If
            'Next

            If (ConsoleOrService = "service") Then
                '******RUN AS A SERVICE - INCLUDE THIS CODE************************
                Dim thrMyThread As New System.Threading.Thread(AddressOf UDPlisten)
                thrMyThread.Start()
            ElseIf (ConsoleOrService = "console") Then
                '******RUN AS A CONSOLE - INCLUDE THIS CODE************************
                UDPlisten()
            End If

        Catch ex As Exception
            'Console.WriteLine(ex.ToString())
            WriteToLog(ex.ToString(), True)
        End Try
    End Sub
    Private Function InlineAssignHelper(Of T)(ByRef target As T, ByVal value As T) As T
        target = value
        Return value
    End Function
    Private Sub writeData(ByVal strData As String)

        Dim strTimeStamp As String = Format(Now.ToString("yyyyMMdd"))
        Dim strFilename As String = "C:\mimics" & strTimeStamp & ".txt"
        If Not File.Exists(strFilename) Then
            Dim s As New StreamWriter(strFilename)
            s.WriteLine("Created: " & Now.ToString("dd MMM yyyy HH:mm:ss"))
            s.WriteLine("Created by the CMM mimics")
            s.WriteLine("-------------------------------------------------------------------------------------")
            s.Close()
        End If

        Dim sw As New StreamWriter(strFilename, True)
        sw.WriteLine(strData)
        sw.Close()

    End Sub
   
    Private Sub writeMySQL(ByVal sz As String)

        '60 43 ED 15 00 00  
        'E8 07 37 14 00 00
        '12 4C ED 15 00 00

        '010402030209060008006003F201CC03FD03FC03FC000000000000AA0000000000000000000000000000000000000FFF000000000000000000000000000000000000000000000000000003F00000000000000000000000000000FFFF //with RPM
        '123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890
        '         1         2         3         4         5         6         7         8         9         0         1         2         3         4         5         6         7         8
        Try
            'The incoming unix timestamp stamps the datetime of the cpu unit's reading
            'which is prone to error due to older units not updating time correctly.
            'The datetime written to the table here reflects the datetime
            'Use the server's date time stamp for the incoming packet until all of the cpu units are getting the time correctly
            '-------------------------------------------------OMIT UNTIL TIME CORRECTED----------------------------------------
            'Dim strUnix As String
            'Dim q As Integer
            'For q = 2 To 20 Step 2
            '    strUnix += Mid(sz, q, 1)
            'Next

            'If Len(strUnix) <> 10 Then strUnix = 0
            'lngUnix = CLng(strUnix)
            'If lngUnix = 0 Then lngUnix = 1388534400 '01/01/2014 00:00:00
            'If lngUnix > 1388534400 Then lngUnix += 7200
            '-------------------------------------------------------------------------------------------------------------------

            Dim lngUnix As Long = (DateTime.UtcNow() - New DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds
            lngUnix = lngUnix + 7200 'Work in local time 

            Dim strDate As String = mimicDate(lngUnix)
            Dim datDate As Date = CDate(strDate)
            Dim strTime As String = mimicTime(lngUnix)

            Dim strDevice As String = strSubnet & CStr(Convert.ToInt32(Mid(sz, 21, 2), 16))
            Dim strUnique As String = CStr(lngUnix) & strDevice
            Dim dblUnique As Double = (lngUnix - 1000000000) + Convert.ToInt32(Mid(sz, 21, 2), 16) / 1000

            If (Len(sz)) < 175 Then Exit Sub 'Pre accelerometer(2) versions

            If Left(sz, 1) = "3" Then '"3" indicator for a status messsage
                Dim intCelsius As Integer = Convert.ToInt32(Mid(sz, 23, 2), 16)
                Dim intWiFiStrength As Integer = Convert.ToInt32(Mid(sz, 25, 2), 16)
                Dim strSSID As String = Convert.ToChar(Convert.ToUInt32(Mid(sz, 27, 2), 16))
                Dim strHex As String
                Dim strChar As Char

                For q = 29 To Len(sz) Step 2
                    strHex = Mid(sz, q, 2)
                    If (strHex = "00" Or strHex = "FF") Then Exit For
                    strChar = Convert.ToChar(Convert.ToUInt32(strHex, 16))
                    strSSID = strSSID + strChar
                Next

                strSSID = Trim(strSSID)
                Dim strStatus As String = "SSID: " & strSSID & " [Strength: " & intWiFiStrength & "] [Temperature: " & intCelsius & "]"

                strSql = "INSERT INTO `cmm`.`tblmimics_status` (datDate, strTime, strDevice, strStatus)" _
                & " VALUES ('" & strDate & "', '" & strTime & "', '" & strDevice & "', '" & strStatus & "')"

            ElseIf Left(sz, 1) = "0" Then '"0" indicator for a data logging messsage

                Dim A0 As Double = (Convert.ToInt32(Mid(sz, 23, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 25, 2), 16)
                Dim A1 As Double = (Convert.ToInt32(Mid(sz, 27, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 29, 2), 16)
                Dim A2 As Double = (Convert.ToInt32(Mid(sz, 31, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 33, 2), 16)
                Dim A3 As Double = (Convert.ToInt32(Mid(sz, 35, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 37, 2), 16)
                Dim A4 As Double = (Convert.ToInt32(Mid(sz, 39, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 41, 2), 16)
                Dim A5 As Double = (Convert.ToInt32(Mid(sz, 43, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 45, 2), 16)
                Dim A6 As Double = (Convert.ToInt32(Mid(sz, 47, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 49, 2), 16)
                Dim A7 As Double = (Convert.ToInt32(Mid(sz, 51, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 53, 2), 16)
                Dim A8 As Double = (Convert.ToInt32(Mid(sz, 55, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 57, 2), 16)
                Dim A9 As Double = (Convert.ToInt32(Mid(sz, 59, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 61, 2), 16)
                Dim A10 As Double = (Convert.ToInt32(Mid(sz, 63, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 65, 2), 16)
                Dim A11 As Double = (Convert.ToInt32(Mid(sz, 67, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 69, 2), 16)
                Dim A12 As Double = (Convert.ToInt32(Mid(sz, 71, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 73, 2), 16)
                Dim A13 As Double = (Convert.ToInt32(Mid(sz, 75, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 77, 2), 16)
                Dim A14 As Double = (Convert.ToInt32(Mid(sz, 79, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 81, 2), 16)
                Dim A15 As Double = (Convert.ToInt32(Mid(sz, 83, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 85, 2), 16)

                Dim Ext As Double = (Convert.ToInt32(Mid(sz, 87, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 89, 2), 16)

                'Incoming integer includes one decimal place
                A0 = (A0 / 10)
                A1 = (A1 / 10)
                A2 = (A2 / 10)
                A3 = (A3 / 10)
                A4 = (A4 / 10)
                A5 = (A5 / 10)

                A6 = (A6 / 10)
                A7 = (A7 / 10)
                A8 = (A8 / 10)
                A9 = (A9 / 10)
                A10 = (A10 / 10)
                A11 = (A11 / 10)
                A12 = (A12 / 10)
                A13 = (A13 / 10)
                A14 = (A14 / 10)
                A15 = (A15 / 10)

                If blnFractions = False Then
                    A0 = CInt(A0)
                    A1 = CInt(A1)
                    A2 = CInt(A2)
                    A3 = CInt(A3)
                    A4 = CInt(A4)
                    A5 = CInt(A5)
                    A6 = CInt(A6)
                    A7 = CInt(A7)
                    A8 = CInt(A8)
                    A9 = CInt(A9)
                    A10 = CInt(A10)
                    A11 = CInt(A11)
                    A12 = CInt(A12)
                    A13 = CInt(A13)
                    A14 = CInt(A14)
                    A15 = CInt(A15)
                End If

                Dim bitArray_1 As New BitArray(System.BitConverter.GetBytes(Convert.ToInt32(Mid(sz, 91, 2), 16)))
                Dim bitArray_2 As New BitArray(System.BitConverter.GetBytes(Convert.ToInt32(Mid(sz, 93, 2), 16)))
                Dim bitArray_3 As New BitArray(System.BitConverter.GetBytes(Convert.ToInt32(Mid(sz, 95, 2), 16)))

                Dim L1 As Integer = -(bitArray_1(7))
                Dim L2 As Integer = -(bitArray_1(6))
                Dim L3 As Integer = -(bitArray_1(5))
                Dim L4 As Integer = -(bitArray_1(4))
                Dim D0 As Integer = -(bitArray_1(3))
                Dim D1 As Integer = -(bitArray_1(2))
                Dim D2 As Integer = -(bitArray_1(1))
                Dim D3 As Integer = -(bitArray_1(0))

                Dim D4 As Integer = -(bitArray_2(7))
                Dim D5 As Integer = -(bitArray_2(6))
                Dim D6 As Integer = -(bitArray_2(5))
                Dim D7 As Integer = -(bitArray_2(4))
                Dim B1 As Integer = -(bitArray_2(3))
                Dim B2 As Integer = -(bitArray_2(2))
                Dim B3 As Integer = -(bitArray_2(1))
                Dim B4 As Integer = -(bitArray_2(0))

                Dim D8 As Integer = -(bitArray_3(7))
                Dim D9 As Integer = -(bitArray_3(6))
                Dim D10 As Integer = -(bitArray_3(5))
                Dim D11 As Integer = -(bitArray_3(4))
                Dim D12 As Integer = -(bitArray_3(3))
                Dim D13 As Integer = -(bitArray_3(2))
                Dim D14 As Integer = -(bitArray_3(1))
                Dim D15 As Integer = -(bitArray_3(0))

                Dim strPeripheral_1 As String = Mid(sz, 97, 12)
                Dim strPeripheral_2 As String = Mid(sz, 109, 12)
                Dim strPeripheral_3 As String = Mid(sz, 121, 12)

                Dim acc0 As Integer = CStr(Convert.ToInt32(Mid(sz, 133, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 135, 2), 16)
                Dim acc1 As Integer = CStr(Convert.ToInt32(Mid(sz, 137, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 139, 2), 16)
                Dim acc2 As Integer = CStr(Convert.ToInt32(Mid(sz, 141, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 143, 2), 16)
                Dim acc3 As Integer = CStr(Convert.ToInt32(Mid(sz, 145, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 147, 2), 16)
                Dim acc4 As Integer = CStr(Convert.ToInt32(Mid(sz, 149, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 151, 2), 16)
                Dim acc5 As Integer = CStr(Convert.ToInt32(Mid(sz, 153, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 155, 2), 16)

                Dim acc6 As Integer = CStr(Convert.ToInt32(Mid(sz, 157, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 159, 2), 16)
                Dim acc7 As Integer = CStr(Convert.ToInt32(Mid(sz, 161, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 163, 2), 16)
                Dim acc8 As Integer = CStr(Convert.ToInt32(Mid(sz, 165, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 167, 2), 16)
                Dim acc9 As Integer = CStr(Convert.ToInt32(Mid(sz, 169, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 171, 2), 16)
                Dim acc10 As Integer = CStr(Convert.ToInt32(Mid(sz, 173, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 175, 2), 16)
                Dim acc11 As Integer = CStr(Convert.ToInt32(Mid(sz, 177, 2), 16) << 8) + Convert.ToInt32(Mid(sz, 179, 2), 16)

                strSql = "INSERT INTO `cmm`.`tblmimics` (lngUnix, datDate, strTime, strDevice," _
                    & " A0, A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, Ext," _
                    & " L1, L2, L3, L4, D0, D1, D2, D3, D4, D5, D6, D7, B1, B2, B3, B4, D8, D9, D10, D11, D12, D13, D14, D15," _
                    & " Peripheral_1, Peripheral_2, Peripheral_3, x_max, x_min, y_max, y_min, z_max, z_min, x_max_2, x_min_2, y_max_2, y_min_2, z_max_2, z_min_2, dblUnique)" _
                    & " VALUES (" & lngUnix & ", '" & strDate & "', '" & strTime & "', '" & strDevice & "'," _
                    & "" & A0 & ", " & A1 & ", " & A2 & ", " & A3 & ", " & A4 & ", " & A5 & ", " & A6 & ", " & A7 & "," _
                    & "" & A8 & ", " & A9 & ", " & A10 & ", " & A11 & ", " & A12 & ", " & A13 & ", " & A14 & ", " & A15 & "," _
                    & "" & Ext & "," _
                    & "" & L1 & ", " & L2 & ", " & L3 & ", " & L4 & ", " _
                    & "" & D0 & ", " & D1 & ", " & D2 & ", " & D3 & ", " & D4 & ", " & D5 & ", " & D6 & ", " & D7 & ", " _
                    & "" & B1 & ", " & B2 & ", " & B3 & ", " & B4 & ", " _
                    & "" & D8 & ", " & D9 & ", " & D10 & ", " & D11 & ", " & D12 & ", " & D13 & ", " & D14 & ", " & D15 & ", " _
                    & "'" & strPeripheral_1 & "', '" & strPeripheral_2 & "', '" & strPeripheral_3 & "', " _
                    & "" & acc0 & ", " & acc1 & ", " & acc2 & ", " & acc3 & ", " & acc4 & ", " & acc5 & ", " _
                    & "" & acc6 & ", " & acc7 & ", " & acc8 & ", " & acc9 & ", " & acc10 & ", " & acc11 & ", " & dblUnique & ")"

                strSqlINSERTholding = "INSERT INTO `cmm`.`tblmimics_holding` (lngUnix, datDate, strTime, strDevice," _
                    & " A0, A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, Ext," _
                    & " L1, L2, L3, L4, D0, D1, D2, D3, D4, D5, D6, D7, B1, B2, B3, B4, D8, D9, D10, D11, D12, D13, D14, D15," _
                    & " Peripheral_1, Peripheral_2, Peripheral_3, x_max, x_min, y_max, y_min, z_max, z_min, x_max_2, x_min_2, y_max_2, y_min_2, z_max_2, z_min_2, dblUnique)" _
                    & " VALUES (" & lngUnix & ", '" & strDate & "', '" & strTime & "', '" & strDevice & "'," _
                    & "" & A0 & ", " & A1 & ", " & A2 & ", " & A3 & ", " & A4 & ", " & A5 & ", " & A6 & ", " & A7 & "," _
                    & "" & A8 & ", " & A9 & ", " & A10 & ", " & A11 & ", " & A12 & ", " & A13 & ", " & A14 & ", " & A15 & "," _
                    & "" & Ext & "," _
                    & "" & L1 & ", " & L2 & ", " & L3 & ", " & L4 & ", " _
                    & "" & D0 & ", " & D1 & ", " & D2 & ", " & D3 & ", " & D4 & ", " & D5 & ", " & D6 & ", " & D7 & ", " _
                    & "" & B1 & ", " & B2 & ", " & B3 & ", " & B4 & ", " _
                    & "" & D8 & ", " & D9 & ", " & D10 & ", " & D11 & ", " & D12 & ", " & D13 & ", " & D14 & ", " & D15 & ", " _
                    & "'" & strPeripheral_1 & "', '" & strPeripheral_2 & "', '" & strPeripheral_3 & "', " _
                    & "" & acc0 & ", " & acc1 & ", " & acc2 & ", " & acc3 & ", " & acc4 & ", " & acc5 & ", " _
                    & "" & acc6 & ", " & acc7 & ", " & acc8 & ", " & acc9 & ", " & acc10 & ", " & acc11 & ", " & dblUnique & ")"

                strSqlUPDATEholding = "UPDATE `cmm`.`tblmimics_holding` SET lngUnix = " & lngUnix & ", datDate = '" & strDate & "', strTime = '" & strTime & "'," _
                    & " A0 = " & A0 & ", A1 = " & A1 & ", A2 = " & A2 & ", A3 = " & A3 & ", A4 = " & A4 & ", A5 = " & A5 & ", A6 = " & A6 & ", A7 = " & A7 & ", A8 = " & A8 & "," _
                    & " A9 = " & A9 & ", A10 = " & A10 & ", A11 = " & A11 & ", A12 = " & A12 & ", A13 = " & A13 & ", A14 = " & A14 & ", A15 = " & A15 & ", Ext = " & Ext & ", " _
                    & " L1 = " & L1 & ", L2 = " & L2 & ", L3 = " & L3 & ", L4 = " & L4 & ", D0 = " & D0 & ", D1 = " & D1 & ", D2 = " & D2 & ", D3 = " & D3 & ", " _
                    & " D4 = " & D4 & ", D5 = " & D5 & ", D6 = " & D6 & ", D7 = " & D7 & ", D8 = " & D8 & ", D9 = " & D9 & ", D10 = " & D10 & ", D11 = " & D11 & ", " _
                    & " D12 = " & D12 & ", D13 = " & D13 & ", D14 = " & D14 & ", D15 = " & D15 & ", " _
                    & " Peripheral_1 = '" & strPeripheral_1 & "', Peripheral_2 = '" & strPeripheral_2 & "', Peripheral_3 = '" & strPeripheral_3 & "', " _
                    & " x_max = " & acc0 & ", x_min = " & acc1 & ", y_max = " & acc2 & ", y_min = " & acc3 & ", z_max = " & acc4 & ", z_min = " & acc5 & ", " _
                    & " x_max_2 = " & acc6 & ", x_min_2 = " & acc7 & ", y_max_2 = " & acc8 & ", y_min_2 = " & acc9 & ", z_max_2 = " & acc10 & ", z_min_2 = " & acc11 & ", dblUnique = " & dblUnique & " WHERE strDevice = '" & strDevice & "'"
            End If

            If MySqlCon.State = ConnectionState.Closed Then MySqlCon.Open()
            If MySqlCon.State = ConnectionState.Broken Then MySqlCon.Open()

            Dim sqlSelectCMD As MySqlCommand

            'If this IP address exists in the mimics table then do not re insert
            Dim dbcmd As New MySqlCommand("SELECT tblmimics.strDevice FROM tblmimics WHERE tblmimics.dblUnique =  " & dblUnique, MySqlCon)
            Dim blnAlready As Boolean = False
            If (RowCount(dbcmd) > 0) Then blnAlready = True
            dbcmd = Nothing

            If blnAlready = False Then
                'Insert new data line into tblMimics
                strStep = "SQL"
                sqlSelectCMD = New MySqlCommand(strSql, MySqlCon)
                sqlSelectCMD.ExecuteScalar()
            End If

            'If this IP address exists in the holding table then UPDATE and, if not, then INSERT
            strSql = "SELECT tblmimics_holding.strDevice FROM tblmimics_holding WHERE tblmimics_holding.strDevice = '" & strDevice & "'"
            dbcmd = New MySqlCommand(strSql, MySqlCon)
            Dim blnExists As Boolean = False
            If (RowCount(dbcmd) > 0) Then blnExists = True
            dbcmd = Nothing

            If Left(sz, 1) = "0" Then '"0" indicator for a data logging messsage
                If (blnExists = False) Then 'INSERT
                    strStep = "INSERT"
                    sqlSelectCMD = New MySqlCommand(strSqlINSERTholding, MySqlCon)
                    sqlSelectCMD.ExecuteScalar()
                Else                         'UPDATE
                    strStep = "UPDATE"
                    sqlSelectCMD = New MySqlCommand(strSqlUPDATEholding, MySqlCon)
                    sqlSelectCMD.ExecuteScalar()
                End If
            End If


        Catch ex As Exception
            If MySqlCon.State = ConnectionState.Closed Then MySqlCon.Open()
            If MySqlCon.State = ConnectionState.Broken Then MySqlCon.Open()
            WriteToLog(strStep)
            WriteToLog("strsql:" & strSql)
            WriteToLog("strSqlINSERTholding:" & strSqlINSERTholding)
            WriteToLog("strSqlUPDATEholding:" & strSqlUPDATEholding)
            WriteToLog(ex.ToString(), True)
        End Try

    End Sub

    Private Function GetIni(ByVal strItem As String) As String

        Try
            Dim strValue, strParameter, inpLine, strResult, strAppPath As String
            Dim n, x As Integer

            Dim strFilename As String = "C:\CMM mimics\mimics.ini"
            If Not File.Exists(strFilename) Then WriteToLog("Error: " & strFilename & " not found.")

            ' Create an instance of StreamReader to read from a file.
            Dim sr As StreamReader = New StreamReader(strFilename)
            Dim strLine As String

            Do Until strValue = "[END]" Or x > 1000
                strValue = sr.ReadLine
                n = 1
                Do Until Right(inpLine, 1) = "="
                    inpLine = (Left(strValue, n))
                    If n > Len(strValue) Then GoTo NextLine
                    n = n + 1
                Loop
                strParameter = (Left(inpLine, n - 2))
                If (strParameter) = (strItem) Then
                    sr.Close()
                    Return Trim((Right(strValue, Len(strValue) - n + 1)))
                Else
                    inpLine = "Nothing"
                End If
NextLine:
                x = x + 1
            Loop

            sr.Close()

            Return strResult

        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try

    End Function
    Private Sub WriteToLog(ByVal strMessage As String, Optional ByVal blnLine As Boolean = False)

        Try
            Dim strFilename As String = "C:\CMM mimics\mimics.log"
            Dim sw As StreamWriter

            'Check that the log file exists and create it if it is absent
            If Not File.Exists(strFilename) Then
                sw = New StreamWriter(strFilename)
                sw.WriteLine("CMM mimics log file")
                sw.WriteLine("Created: " & Now.ToString("dd MMM yyyy HH:mm:ss"))
                sw.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------")
                sw.Close()
                sw = Nothing
            End If

            'Write the received message to the log file
            sw = File.AppendText(strFilename)
            sw.WriteLine(Now.ToString("dd MMM yyyy HH:mm:ss") & " : " & strMessage)
            If blnLine = True Then sw.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------")
            sw.Close()
            sw = Nothing


        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try

    End Sub
    Public Function GetValue(ByVal strLine As String, ByVal intField As Integer) As String
        Try

            'Will return the string value that is in position intField, in the strLine file
            Dim strSeparator As String = ","
            Dim x As Integer = Len(strLine)
            Dim p, n, y As Integer
            Dim strResult As String

            p = 1
            For n = 1 To x

                If n = x Then
                    strResult = Trim(Mid(strLine, p + 1, n - p))
                    strResult = Trim(Mid(strLine, p, n - p + 1))
                    Exit For
                End If

                If Mid(strLine, n, 1) = strSeparator Or n = x Then
                    y = y + 1
                    If y = intField Then
                        strResult = Trim(Mid(strLine, p, n - p))
                        Exit For
                    Else
                        p = n + 1
                    End If
                End If
            Next

            If Len(strResult) = 0 Then strResult = "999"

            Return strResult
        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try

    End Function
    Private Sub UDPlisten()

        Try
            Const listeningPort As Integer = 44400
            Dim listeningEndPoint As New IPEndPoint(IPAddress.Any, listeningPort)
            Dim udpListener As New UdpClient(listeningEndPoint)
            udpListener.Client.ReceiveTimeout = -1

            'UDP Listening loop
            Do
                Dim rgbIn As [Byte]() = Nothing
                'Console.WriteLine("Listening on port: {0}.", listeningPort.ToString())
                ' now listen for an incoming datagram
                If (InlineAssignHelper(rgbIn, udpListener.Receive(listeningEndPoint))) IsNot Nothing Then
                    Try
                        Dim strHEX As String = ByteArrayToString(rgbIn)
                        strHEX = strHEX.ToUpper
                        Console.WriteLine(strHEX)
                        writeMySQL(strHEX)
                        ' something went wrong, probably not ascii data
                    Catch err As exception
                        'Console.WriteLine("Error in decoding string")
                        WriteToLog("Error in decoding UDP string", True)
                        WriteToLog(err.ToString(), True)
                    End Try

                End If
            Loop While True


        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try


    End Sub
   
    Private Function decode(ByVal strBin As String) As Integer

        Try

            Dim intResult As Int32 = Convert.ToInt32(strBin, 2)
            Return intResult

        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try

    End Function
    Private Function mimicDateTime(ByVal lngEpochTime As Long)

        'unix = 1356998400 ' 01 01 2013 00:00:00
        '1375531467 = 3/08/2013 12:04:27

        Try
            Dim baseDate As New DateTime(1970, 1, 1, 0, 0, 0)
            Dim datDate As Date = baseDate.ToLocalTime().AddSeconds(lngEpochTime)
            Dim strDateTime As String = datDate.ToString(Format("yyyy/MM/dd HH:mm:ss"))
            If (strDateTime.IndexOf("/") = -1) Then
                strDateTime = Left(strDateTime, 4) & "/" & Microsoft.VisualBasic.Mid(strDateTime, 6, 2) & "/" & Microsoft.VisualBasic.Mid(strDateTime, 9, 2)
            End If

            Return strDateTime

        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try
    End Function

    Private Function mimicDate(ByVal lngEpochTime As Long)

        Try

            Dim datDate As Date = DateAdd(DateInterval.Second, lngEpochTime, #1/1/1970#)
            Dim strDate As String = datDate.ToString(Format("yyyy-MM-dd"))

            If (strDate.IndexOf("/") = -1) Then
                strDate = Microsoft.VisualBasic.Left(strDate, 4) & "-" & Microsoft.VisualBasic.Mid(strDate, 6, 2) & "-" & Microsoft.VisualBasic.Mid(strDate, 9, 2)
            End If

            Return strDate

        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try

    End Function
    Private Function mimicTime(ByVal lngEpochTime As Long)

        Try
            Dim datDate As Date = DateAdd(DateInterval.Second, lngEpochTime, #1/1/1970#)
            Dim strTime As String = datDate.ToString(Format("HH:mm:ss"))

            Return strTime

        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try

    End Function
    Private Function ByteArrayToString(ByVal ba As Byte()) As String
        Dim hex As String = BitConverter.ToString(ba)
        Return hex.Replace("-", "")
    End Function
    Private Function ByteArrayToString_II(ByVal ba As Byte()) As String

        Dim hex As New StringBuilder(ba.Length * 2)
        For Each b As Byte In ba
            hex.AppendFormat("{0:x2}", b)
        Next

        Return hex.ToString()

    End Function
    Public Function RowCount(ByVal dbCmd As MySqlCommand) As Integer
        'A clever little wrapper that will return the amount of rows in the datareader
        'that is passed to it

        Dim n As Integer
        Dim sqlReader As MySqlDataReader

        Try
            sqlReader = dbCmd.ExecuteReader()
            While sqlReader.Read()
                n = n + 1
            End While
            sqlReader.Close()

            Return n

        Catch ex As Exception
            WriteToLog(ex.ToString(), True)
        End Try

    End Function


End Class
