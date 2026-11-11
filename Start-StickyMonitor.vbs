Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

rootPath = fso.GetParentFolderName(WScript.ScriptFullName)
exePath = fso.BuildPath(rootPath, "dist\app\StickyMonitor.App.exe")

If Not fso.FileExists(exePath) Then
    MsgBox "Sticky Monitor build not found at:" & vbCrLf & exePath, vbExclamation, "Sticky Monitor"
    WScript.Quit 1
End If

command = """" & exePath & """"
shell.Run command, 0, False
