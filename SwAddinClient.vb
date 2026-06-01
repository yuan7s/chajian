Imports System.Net.Http
Imports System.Net.WebSockets
Imports System.Text
Imports System.Threading
Imports System.Web.Script.Serialization

Public Class SwAddinClient
    Implements IDisposable

    Public Event DocChanged(title As String, path As String)
    Public Event SelectionChanged(name As String, type As String)
    Public Event Disconnected()

    Private ReadOnly _http As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}
    Private ReadOnly _json As New JavaScriptSerializer()
    Private ReadOnly _baseUrl As String
    Private ReadOnly _wsUrl As String
    Private _ws As ClientWebSocket
    Private _wsCts As CancellationTokenSource
    Private _pingTimer As Threading.Timer

    Public Sub New(Optional port As Integer = 32128)
        _baseUrl = String.Format("http://127.0.0.1:{0}/", port)
        _wsUrl = String.Format("ws://127.0.0.1:{0}/events", port)
    End Sub

    Public Async Function ConnectAsync() As Task(Of Boolean)
        Try
            Dim pingResult = Await SendCommandAsync("ping")
            If pingResult Is Nothing Then Return False

            _wsCts = New CancellationTokenSource()
            _ws = New ClientWebSocket()
            Await _ws.ConnectAsync(New Uri(_wsUrl), _wsCts.Token)
            StartWsReadLoop()
            Return True
        Catch ex As Exception
            Debug.WriteLine("SwAddinClient: WebSocket failed, falling back to HTTP ping: " & ex.Message)
            StartHttpPolling()
            Return True
        End Try
    End Function

    Private Sub StartWsReadLoop()
        Task.Run(Async Function()
            Dim buffer As Byte() = New Byte(4095) {}
            While _ws IsNot Nothing AndAlso _ws.State = WebSocketState.Open
                Try
                    Dim result = Await _ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), _wsCts.Token)
                    If result.MessageType = WebSocketMessageType.Close Then Exit While
                    If result.MessageType = WebSocketMessageType.Text Then
                        Dim json = Encoding.UTF8.GetString(buffer, 0, result.Count)
                        ProcessWsMessage(json)
                    End If
                Catch ex As Exception
                    Exit While
                End Try
            End While
            Debug.WriteLine("SwAddinClient: WebSocket disconnected, attempting reconnect...")
            Await ReconnectWsAsync()
        End Function)
    End Sub

    Private Async Function ReconnectWsAsync() As Task
        For attempt As Integer = 1 To 3
            Try
                Await Task.Delay(1000 * CInt(Math.Pow(2, attempt - 1)))
                If _ws IsNot Nothing Then _ws.Dispose()
                _ws = New ClientWebSocket()
                If _wsCts IsNot Nothing Then _wsCts.Cancel()
                _wsCts = New CancellationTokenSource()
                Await _ws.ConnectAsync(New Uri(_wsUrl), _wsCts.Token)
                StartWsReadLoop()
                Debug.WriteLine("SwAddinClient: WebSocket reconnected on attempt " & attempt)
                Return
            Catch ex As Exception
                Debug.WriteLine("SwAddinClient: WS reconnect attempt " & attempt & " failed: " & ex.Message)
            End Try
        Next
        Debug.WriteLine("SwAddinClient: WS reconnect exhausted, falling back to HTTP polling")
        RaiseEvent Disconnected()
        StartHttpPolling()
    End Function

    Private Sub StartHttpPolling()
        _pingTimer = New Threading.Timer(
            Async Sub(state)
                Try
                    Dim info = Await SendCommandAsync("active-document")
                    If info IsNot Nothing Then
                        ProcessDocInfo(info)
                    End If
                Catch ex As Exception
                    Debug.WriteLine("SwAddinClient: HTTP poll failed: " & ex.Message)
                End Try
            End Sub, Nothing, 5000, 5000)
    End Sub

    Private Sub ProcessWsMessage(json As String)
        Try
            Dim msg = _json.Deserialize(Of Dictionary(Of String, Object))(json)
            If msg Is Nothing Then Return

            Dim msgType As String = Nothing
            If msg.ContainsKey("type") AndAlso msg("type") IsNot Nothing Then
                msgType = msg("type").ToString()
            End If

            Select Case msgType
                Case "ping"
                Case "doc-changed"
                    Dim data = TryCast(msg("data"), Dictionary(Of String, Object))
                    If data IsNot Nothing Then
                        Dim title As String = ""
                        Dim path As String = ""
                        If data.ContainsKey("title") AndAlso data("title") IsNot Nothing Then
                            title = data("title").ToString()
                        End If
                        If data.ContainsKey("path") AndAlso data("path") IsNot Nothing Then
                            path = data("path").ToString()
                        End If
                        RaiseEvent DocChanged(title, path)
                    End If
                Case "selection-changed"
                    Dim data = TryCast(msg("data"), Dictionary(Of String, Object))
                    If data IsNot Nothing Then
                        Dim name As String = ""
                        Dim sType As String = ""
                        If data.ContainsKey("name") AndAlso data("name") IsNot Nothing Then
                            name = data("name").ToString()
                        End If
                        If data.ContainsKey("type") AndAlso data("type") IsNot Nothing Then
                            sType = data("type").ToString()
                        End If
                        RaiseEvent SelectionChanged(name, sType)
                    End If
                Case "sw-shutdown"
                    RaiseEvent Disconnected()
            End Select
        Catch ex As Exception
            Debug.WriteLine("SwAddinClient: WS message parse error: " & ex.Message)
        End Try
    End Sub

    Private Sub ProcessDocInfo(info As Object)
        Try
            Dim dict = TryCast(info, Dictionary(Of String, Object))
            If dict IsNot Nothing Then
                Dim title As String = ""
                Dim path As String = ""
                If dict.ContainsKey("title") AndAlso dict("title") IsNot Nothing Then
                    title = dict("title").ToString()
                End If
                If dict.ContainsKey("path") AndAlso dict("path") IsNot Nothing Then
                    path = dict("path").ToString()
                End If
                RaiseEvent DocChanged(title, path)
            End If
        Catch
        End Try
    End Sub

    Public Async Function SendCommandAsync(command As String, Optional args As Dictionary(Of String, Object) = Nothing) As Task(Of Object)
        Try
            Dim req = New Dictionary(Of String, Object) From {
                {"Command", command},
                {"Args", If(args, New Dictionary(Of String, Object)())}
            }
            Dim json = _json.Serialize(req)
            Dim content = New StringContent(json, Encoding.UTF8, "application/json")
            Dim response = Await _http.PostAsync(_baseUrl & "command", content)
            Dim body = Await response.Content.ReadAsStringAsync()
            Dim result = _json.Deserialize(Of Dictionary(Of String, Object))(body)

            If result IsNot Nothing AndAlso result.ContainsKey("ok") AndAlso CBool(result("ok")) Then
                Return If(result.ContainsKey("data"), result("data"), Nothing)
            Else
                Dim errMsg As String = "未知错误"
                If result IsNot Nothing AndAlso result.ContainsKey("error") AndAlso result("error") IsNot Nothing Then
                    errMsg = result("error").ToString()
                End If
                Throw New InvalidOperationException(errMsg)
            End If
        Catch ex As InvalidOperationException
            Throw
        Catch ex As Exception
            Throw New InvalidOperationException("通信失败: " & ex.Message, ex)
        End Try
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _pingTimer IsNot Nothing Then
            _pingTimer.Dispose()
            _pingTimer = Nothing
        End If
        If _wsCts IsNot Nothing Then
            _wsCts.Cancel()
        End If
        If _ws IsNot Nothing Then
            Try
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(1000)
            Catch
            End Try
            _ws.Dispose()
            _ws = Nothing
        End If
        If _http IsNot Nothing Then
            _http.Dispose()
        End If
    End Sub
End Class
