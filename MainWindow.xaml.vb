Imports System.Net
Imports System.Reflection
Imports System.Security.Policy
Imports Newtonsoft.Json
Imports RestSharp
Imports System.Windows.Navigation

Class MainWindow
    ' On application load
    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs) Handles MainWindow.Loaded
        ' Pull version number from the assembly and push it to the About tab
        Dim version As String = GetAssemblyVersion()
        VersionRun.Text = version

        ' populate the settings tab text fields from saved settings.
        Input_Host.Text = My.Settings.Host
        Input_Username.Text = My.Settings.Username
        Input_Password.Password = My.Settings.Password
    End Sub
    ' This sub made by ChatGPT
    Private Sub Hyperlink_RequestNavigate(sender As Object, e As RequestNavigateEventArgs)
        Dim hyperlink As Hyperlink = DirectCast(sender, Hyperlink)
        Dim url As String = hyperlink.NavigateUri.ToString()

        ' Open the URL in the default web browser
        Try
            Process.Start(New ProcessStartInfo With {
            .FileName = url,
            .UseShellExecute = True
        })
        Catch ex As Exception
            ' Handle any exceptions that might occur
            MessageBox.Show($"An error occurred while opening the URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try

        e.Handled = True
    End Sub

    Private Function GetAssemblyVersion() As String
        Dim assembly As Assembly = Assembly.GetExecutingAssembly()
        Dim versionAttribute As Version = assembly.GetName().Version

        Return versionAttribute.ToString()
    End Function
    ' Enable on button click.
    Private Sub Button_Enable_Click(sender As Object, e As RoutedEventArgs) Handles Button_Enable.Click
        Dim Task = API_Enable()
        Dim status = API_Status()

        Dim OutputString = EnableDisable_String(status, Task)
        WriteOutput(OutputString)
    End Sub
    ' Disable on button click.
    Private Sub Button_Disable_Click(sender As Object, e As RoutedEventArgs) Handles Button_Disable.Click
        Dim Task = API_Disable(Input_Duration.Text)
        Dim status = API_Status()

        Dim OutputString = EnableDisable_String(status, Task)
        WriteOutput(OutputString)
    End Sub
    ' Check status & versions on button click.
    Private Sub Button_Status_Click(sender As Object, e As RoutedEventArgs) Handles Button_Status.Click
        Dim status = API_Status()

        Dim OutputString = EnableDisable_String(status)
        WriteOutput(OutputString)

        Update_Check(status.Item("version"), API_Version)
    End Sub
    ' Save settings fields settings storage on button click.
    Private Sub Button_Save_Click(sender As Object, e As RoutedEventArgs) Handles Button_Save.Click
        My.Settings.Host = Input_Host.Text
        My.Settings.Username = Input_Username.Text
        My.Settings.Password = Input_Password.Password
        My.Settings.Save()
    End Sub
    Function EnableDisable_String(status, Optional Task = "")
        Dim OutputString = ""

        If Not Task Like "" Then
            If Task Then
                OutputString = "OK: "
            Else
                OutputString = "ERR: "
            End If
        End If

        If status.Item("enabled") Then
            OutputString &= "ON"
        Else
            OutputString &= "OFF for " & status.Item("duration") & "s"
        End If

        Return OutputString
    End Function
    ' Encode username and password in base64 as username:password for AdGuard Home's API.
    Function Base64()
        Return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(My.Settings.Username & ":" & My.Settings.Password))
    End Function
    ' Write to the text box, and ensure it always scrolls down to show the latest lines.
    Function WriteOutput(Text)
        Status.Text &= Environment.NewLine & Text
        Status_Scroll.ScrollToBottom()
    End Function
    Function Version_Conversion(input As String)
        ' Remove the leading v.
        input = input.Trim("v")

        ' Parse it into a Version object.
        Dim output As Version = Version.Parse(input)

        Return output
    End Function
    ' Compare currently-running and latest-available versions of AdGuard to see if an update is available.
    Function Update_Check(current, latest)
        current = Version_Conversion(current)
        latest = Version_Conversion(latest)
        If latest.CompareTo(current) > 0 Then
            WriteOutput("An update is")
            WriteOutput("available.")
            Return True
        Else
            Return False
        End If
    End Function
    ' Bootstrap function for common API call code
    Function API_Request(method, Optional payload = "")
        ' Create the object that handles the API call.
        Dim request As RestRequest = New RestRequest
        ' Set the method
        request.Method = method
        ' Add necessary headers to the API call.
        request.AddHeader("content-type", "application/json")
        request.AddHeader("accept", "*/*")
        request.AddHeader("authorization", "Basic " & Base64())

        ' Check if payload has been set
        If Not payload Like "" Then
            ' Add the parameter with the payload
            request.AddParameter("application/json", payload, ParameterType.RequestBody)
        End If

        Return request
    End Function
    Function API_Enable()
        ' payload is the json body for POST requests.
        Dim payload As String = "{""enabled"": true }"

        ' Bootstrap API call object creation
        Dim request = API_Request(1, payload)

        ' Set the URL with the API endpoint to hit
        Dim url As String = My.Settings.Host & "/control/protection"

        ' Create the client for the API call
        Dim client = New RestClient(url)

        ' Dim result As IRestResponse = client.Execute(request)
        Dim response As RestResponse = client.Execute(request)

        If response.Content.Contains("OK") Then
            Return True
        Else
            Return False
        End If
    End Function
    Function API_Disable(Duration)
        ' GUI provides duration in seconds, but AdGuard parses it as ms, so we need to multiply it.
        Duration = Duration * 1000
        ' payload is the json body for POST requests.
        Dim payload As String = "{""enabled"": false, ""duration"": " & Duration & "}"

        ' Bootstrap API call object creation
        Dim request = API_Request(1, payload)

        ' Set the URL with the API endpoint to hit
        Dim url As String = My.Settings.Host & "/control/protection"

        ' Create the client for the API call
        Dim client = New RestClient(url)

        ' Dim result As IRestResponse = client.Execute(request)
        Dim response As RestResponse = client.Execute(request)

        If response.Content.Contains("OK") Then
            Return True
        Else
            Return False
        End If
    End Function
    Function API_Status()
        ' Bootstrap API call object creation
        Dim request = API_Request(0)

        ' Set the URL with the API endpoint to hit
        Dim url As String = My.Settings.Host & "/control/status"

        ' Create the client for the API call
        Dim client = New RestClient(url)

        ' Dim result As IRestResponse = client.Execute(request)
        Dim response As RestResponse = client.Execute(request)

        ' Deserialise the response json into an object
        Dim response_json = JsonConvert.DeserializeObject(response.Content)

        Dim status As New Dictionary(Of String, Object)

        ' Check if protection is enabled or disabled
        If response_json.Item("protection_enabled") Like "True" Then
            status.Add("enabled", True)
        ElseIf response_json.Item("protection_enabled") Like "False" Then
            status.Add("enabled", False)
            status.Add("duration", response_json.Item("protection_disabled_duration") / 1000)
        End If

        ' Retrieve the version value.
        status.Add("version", response_json.Item("version"))

        Return status
    End Function
    Function API_Version()
        ' payload is the json body for POST requests.
        Dim payload As String = "{""recheck_now"": true }"

        ' Bootstrap API call object creation
        Dim request = API_Request(1, payload)

        ' Set the URL with the API endpoint to hit
        Dim url As String = My.Settings.Host & "/control/version.json"

        ' Create the client for the API call
        Dim client = New RestClient(url)

        ' Dim result As IRestResponse = client.Execute(request)
        Dim response As RestResponse = client.Execute(request)

        ' Deserialise the response json into an object
        Dim response_json = JsonConvert.DeserializeObject(response.Content)

        ' Retrieve the version value.
        Return response_json.Item("new_version")
    End Function
End Class