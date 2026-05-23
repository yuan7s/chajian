Imports Wpf = System.Windows

Public Module Program
    <STAThread>
    Public Sub Main()
        Dim app As New Wpf.Application()
        app.ShutdownMode = Wpf.ShutdownMode.OnExplicitShutdown
        app.Run(New Form1())
    End Sub
End Module
