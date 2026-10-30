Imports System
Imports System.Data
Imports System.Data.OleDb
Imports System.Drawing
Imports System.Windows.Forms
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Diagnostics
Imports Microsoft.VisualBasic

' ======================================================================
' MODULE: Program
' Description: The main entry point of the VB.NET WinForms application.
' It initializes visual styling, checks connection, and runs MainForm.
' ======================================================================
Public Module Program
    Public SessionUser As String = ""
    Public SessionRole As String = ""
    Public BaseFontSize As Single = 10.0F
    Public connString As String = ""
    Public currentLibrary As String = "Default Library"
    Public IsLocked As Boolean = True
    Public TouchMode As Boolean = False
    Public AppPassword As String = "admin123"
    Public LogoutRequested As Boolean = False
    
    Public listener As HttpListener = Nothing
    
    <STAThread>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        
        LoadConnectionSettings()
        InitializeDatabase()
        
        ' Load settings from database
        ThemeManager.SetTheme(GetSetting("AppTheme", "Light Ivory"))
        BaseFontSize = Convert.ToSingle(GetSetting("BaseFontSize", "10.0"))
        TouchMode = (GetSetting("TouchMode", "False") = "True")
        
        ' Start HTTP Server if enabled
        If GetSetting("WebServerEnabled", "True") = "True" Then
            Task.Run(Sub() StartWebServer())
        End If
        
        Dim showApp As Boolean = True
        While showApp
            LogoutRequested = False
            Using login As New LoginForm()
                If login.ShowDialog() = DialogResult.OK Then
                    SessionUser = login.UserName
                    SessionRole = login.UserRole
                    Application.Run(New MainForm())
                    If Not LogoutRequested Then
                        showApp = False
                    End If
                Else
                    showApp = False
                End If
            End Using
        End While
        
        ' Clean up
        StopWebServer()
    End Sub
    
    Private Sub LoadConnectionSettings()
        Dim configPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt")
        If File.Exists(configPath) Then
            connString = File.ReadAllText(configPath).Trim()
        Else
            connString = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=Library.mdb;Jet OLEDB:Database Password=dbpass;"
            File.WriteAllText(configPath, connString)
        End If
    End Sub
    
    Public Sub InitializeDatabase()
        If String.IsNullOrEmpty(connString) Then Return
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                ' Ensure Libraries table structure exists
                Try
                    Dim cmd As New OleDbCommand("CREATE TABLE Libraries (LibName VARCHAR(255) PRIMARY KEY)", conn)
                    cmd.ExecuteNonQuery()
                Catch
                End Try

                ' Ensure Books table has LibraryName sorting
                Try
                    Dim cmd As New OleDbCommand("ALTER TABLE Books ADD COLUMN LibraryName VARCHAR(255)", conn)
                    cmd.ExecuteNonQuery()
                Catch
                End Try

                ' ADD NEW COLUMNS
                Dim newCols As String() = {
                    "[Author2] VARCHAR(255)", "[Author3] VARCHAR(255)", "[Author4] VARCHAR(255)", 
                    "[Author5] VARCHAR(255)", "[Author6] VARCHAR(255)", "[Author7] VARCHAR(255)", 
                    "[Author8] VARCHAR(255)", "[Author9] VARCHAR(255)", "[Author10] VARCHAR(255)", 
                    "[Pages] INTEGER", "[PubYear] INTEGER", "[CoAuthors] VARCHAR(255)", 
                    "[Publisher] VARCHAR(255)", "[ISBN] VARCHAR(50)", "[CopyNum] INTEGER", 
                    "[Series] VARCHAR(255)", "[CoverPath] VARCHAR(255)", "[BackPath] VARCHAR(255)", 
                    "[Genre] VARCHAR(255)", "[Description] MEMO", "[Language] VARCHAR(50)", 
                    "[Weight] VARCHAR(50)", "[Dimensions] VARCHAR(100)", "[LCCN] VARCHAR(50)", 
                    "[OCLC] VARCHAR(50)", "[Subjects] MEMO", "[ScannedPages] MEMO"
                }
                For Each col In newCols
                    Try
                        Dim cmdCol As New OleDbCommand("ALTER TABLE Books ADD COLUMN " & col, conn)
                        cmdCol.ExecuteNonQuery()
                    Catch
                    End Try
                Next

                ' Ensure Settings table exists
                Try
                    Dim cmdS As New OleDbCommand("CREATE TABLE Settings (KeyName VARCHAR(255) PRIMARY KEY, SettingValue VARCHAR(255))", conn)
                    cmdS.ExecuteNonQuery()
                Catch
                End Try

                ' Create BookCategories
                Try
                    Dim cmdCat As New OleDbCommand("CREATE TABLE BookCategories (CatID AUTOINCREMENT PRIMARY KEY, CategoryName VARCHAR(255) UNIQUE)", conn)
                    cmdCat.ExecuteNonQuery()
                Catch
                End Try

                ' Seed Genres
                Dim checkCountCmd As New OleDbCommand("SELECT COUNT(*) FROM BookCategories", conn)
                If Convert.ToInt32(checkCountCmd.ExecuteScalar()) < 10 Then
                    Dim genresList As String() = {
                        "Fiction", "Non-Fiction", "Science", "History", "Biography", "Fantasy", "Mystery", "Philosophy", "Psychology", "Technology",
                        "Art", "Education", "Law", "Medicine", "Social Science", "Political Science", "Religion", "Language", "Literature", "Computers",
                        "Business", "Economics", "Reference", "Sports", "Music", "Cooking", "Travel", "Poetry", "Drama", "Essays"
                    }
                    For Each g In genresList
                        Try
                            Using cmdI As New OleDbCommand("INSERT INTO BookCategories (CategoryName) VALUES (?)", conn)
                                cmdI.Parameters.AddWithValue("?", g)
                                cmdI.ExecuteNonQuery()
                            End Using
                        Catch
                        End Try
                    Next
                End If

                ' Create Users table
                Try
                    Dim cmdUsers As New OleDbCommand("CREATE TABLE Users (UserID AUTOINCREMENT PRIMARY KEY, Username VARCHAR(50) UNIQUE, Password VARCHAR(50), Role VARCHAR(20))", conn)
                    cmdUsers.ExecuteNonQuery()
                    ' Seed Super Admin
                    Dim cmdAdmin As New OleDbCommand("INSERT INTO Users (Username, Password, Role) VALUES ('admin', '2026', 'SuperAdmin')", conn)
                    cmdAdmin.ExecuteNonQuery()
                Catch
                End Try

                ' Audit and lookup fields
                Try
                    Dim cmdAddBy As New OleDbCommand("ALTER TABLE Books ADD COLUMN AddedBy VARCHAR(50)", conn)
                    cmdAddBy.ExecuteNonQuery()
                Catch
                End Try
                Try
                    Dim cmdEditBy As New OleDbCommand("ALTER TABLE Books ADD COLUMN EditedBy VARCHAR(50)", conn)
                    cmdEditBy.ExecuteNonQuery()
                Catch
                End Try
                Try
                    Dim cmdAddCol As New OleDbCommand("ALTER TABLE Books ADD COLUMN CategoryID INTEGER", conn)
                    cmdAddCol.ExecuteNonQuery()
                Catch
                End Try

                ' Seed default library if none exists
                Dim chkLib As New OleDbCommand("SELECT COUNT(*) FROM Libraries", conn)
                If Convert.ToInt32(chkLib.ExecuteScalar()) = 0 Then
                    Dim cmdLib As New OleDbCommand("INSERT INTO Libraries (LibName) VALUES ('Default Library')", conn)
                    cmdLib.ExecuteNonQuery()
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show("Database Initialization Warning: " & ex.Message)
        End Try
    End Sub
    
    Public Function GetSetting(key As String, defaultVal As String) As String
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT SettingValue FROM Settings WHERE KeyName = ?", conn)
                cmd.Parameters.AddWithValue("?", key)
                Dim res = cmd.ExecuteScalar()
                If res IsNot Nothing AndAlso res IsNot DBNull.Value Then
                    Return res.ToString()
                End If
            End Using
        Catch
        End Try
        Return defaultVal
    End Function

    Public Sub SaveSetting(key As String, val As String)
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmdU As New OleDbCommand("UPDATE Settings SET SettingValue = ? WHERE KeyName = ?", conn)
                cmdU.Parameters.AddWithValue("?", val)
                cmdU.Parameters.AddWithValue("?", key)
                If cmdU.ExecuteNonQuery() = 0 Then
                    Dim cmdI As New OleDbCommand("INSERT INTO Settings (KeyName, SettingValue) VALUES (?, ?)", conn)
                    cmdI.Parameters.AddWithValue("?", key)
                    cmdI.Parameters.AddWithValue("?", val)
                    cmdI.ExecuteNonQuery()
                End If
            End Using
        Catch
        End Try
    End Sub
    
    Public Sub LoadImageSafe(pic As PictureBox, imagePath As String)
        pic.Image = Nothing
        If String.IsNullOrEmpty(imagePath) Then Return
        Try
            If imagePath.StartsWith("http") Then
                Using client As New WebClient()
                    Dim bytes = client.DownloadData(imagePath)
                    Using ms As New MemoryStream(bytes)
                        pic.Image = Image.FromStream(ms)
                        pic.Image = CType(pic.Image.Clone(), Image)
                    End Using
                End Using
            Else
                Dim localPath = imagePath
                If Not Path.IsPathRooted(localPath) Then
                    localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, localPath)
                End If
                If File.Exists(localPath) Then
                    Dim bytes = File.ReadAllBytes(localPath)
                    Using ms As New MemoryStream(bytes)
                        pic.Image = Image.FromStream(ms)
                        pic.Image = CType(pic.Image.Clone(), Image)
                    End Using
                End If
            End If
        Catch
            pic.Image = Nothing
        End Try
    End Sub

    Public Function EnsureLocalImage(id As Integer, url As String, suffix As String) As String
        If String.IsNullOrEmpty(url) OrElse Not url.StartsWith("http") Then Return url
        Try
            Dim dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BookImages")
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
            
            Dim ext = ".jpg"
            If url.ToLower().Contains(".png") Then ext = ".png"
            If url.ToLower().Contains(".gif") Then ext = ".gif"
            
            Dim localName = String.Format("book_{0}_{1}{2}", id, suffix, ext)
            Dim localPath = Path.Combine(dir, localName)
            
            Using client As New WebClient()
                client.DownloadFile(url, localPath)
            End Using
            
            Return "BookImages/" & localName
        Catch
            Return url
        End Try
    End Function

    Public Sub ExportBooksToCsv(libraryName As String)
        Using sfd As New SaveFileDialog()
            sfd.Filter = "CSV files (*.csv)|*.csv"
            sfd.FileName = "Library_Report_" & DateTime.Now.ToString("yyyyMMdd") & ".csv"
            If sfd.ShowDialog() = DialogResult.OK Then
                Try
                    Using conn As New OleDbConnection(connString)
                        conn.Open()
                        Dim cmd As New OleDbCommand("SELECT ID, Title, Author, ISBN, PubYear, Pages, Genre FROM Books WHERE LibraryName = ? ORDER BY ID DESC", conn)
                        cmd.Parameters.AddWithValue("?", libraryName)
                        Dim dt As New DataTable()
                        Using adapter As New OleDbDataAdapter(cmd)
                            adapter.Fill(dt)
                        End Using
                        
                        Using sw As New StreamWriter(sfd.FileName, False, Encoding.UTF8)
                            ' Write headers
                            Dim colNames As New List(Of String)()
                            For Each col As DataColumn In dt.Columns
                                colNames.Add(col.ColumnName)
                            Next
                            sw.WriteLine(String.Join(",", colNames.ToArray()))
                            
                            ' Write data
                            For Each row As DataRow In dt.Rows
                                Dim fields As New List(Of String)()
                                For Each col As DataColumn In dt.Columns
                                    Dim val = row(col).ToString()
                                    val = val.Replace("""", """""")
                                    If val.Contains(",") OrElse val.Contains("""") OrElse val.Contains(vbCrLf) Then
                                        val = """" & val & """"
                                    End If
                                    fields.Add(val)
                                Next
                                sw.WriteLine(String.Join(",", fields.ToArray()))
                            Next
                        End Using
                    End Using
                    MessageBox.Show("Library books exported successfully to CSV!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Export failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    Public Function JsonExtractValue(json As String, key As String) As String
        Try
            Dim pattern As String = """" & key & """\s*:\s*""([^""]*)"""
            Dim m = Regex.Match(json, pattern, RegexOptions.IgnoreCase)
            If m.Success Then
                Return m.Groups(1).Value.Replace(Chr(92) & Chr(34), Chr(34)).Replace(Chr(92) & "/", "/").Trim()
            End If
            Dim patternNum As String = """" & key & """\s*:\s*([0-9\.\+\-eE]+|true|false|null)"
            m = Regex.Match(json, patternNum, RegexOptions.IgnoreCase)
            If m.Success Then
                Dim val = m.Groups(1).Value.Trim()
                If val.ToLower() = "null" Then Return ""
                Return val
            End If
        Catch
        End Try
        Return ""
    End Function

    Public Function GeminiExtractField(json As String, key As String) As String
        Dim pattern As String = """" & key & """\s*:\s*""((?:[^""]|\\"")*)"""
        Dim m = Regex.Match(json, pattern, RegexOptions.IgnoreCase)
        If m.Success Then
            Return m.Groups(1).Value.Replace("\n", " ").Replace(Chr(92) & Chr(34), Chr(34)).Replace(Chr(92) & "/", "/").Trim()
        End If
        Dim patternNum As String = """" & key & """\s*:\s*([0-9]+)"
        m = Regex.Match(json, patternNum, RegexOptions.IgnoreCase)
        If m.Success Then
            Return m.Groups(1).Value.Trim()
        End If
        Return ""
    End Function

    ' --- Web Server Module ---
    Public Sub StartWebServer()
        Try
            If listener IsNot Nothing Then StopWebServer()
            listener = New HttpListener()
            listener.Prefixes.Add("http://localhost:8080/")
            listener.Start()
            While listener.IsListening
                Dim ctx = listener.GetContext()
                Task.Run(Sub() HandleWebRequest(ctx))
            End While
        Catch
        End Try
    End Sub

    Public Sub StopWebServer()
        Try
            If listener IsNot Nothing Then
                listener.Stop()
                listener.Close()
                listener = Nothing
            End If
        Catch
        End Try
    End Sub

    Private Sub HandleWebRequest(ctx As HttpListenerContext)
        Dim req = ctx.Request
        Dim resp = ctx.Response
        Dim rawPath = req.Url.AbsolutePath.ToLower()

        Try
            Dim webDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "web")
            Dim relPath = req.Url.AbsolutePath.TrimStart("/"c).Replace("/"c, Path.DirectorySeparatorChar)
            If String.IsNullOrEmpty(relPath) Then relPath = "index.html"
            Dim localFilePath = Path.Combine(webDir, relPath)

            If File.Exists(localFilePath) Then
                Dim ext = Path.GetExtension(localFilePath).ToLower()
                Dim mime = "application/octet-stream"
                If ext = ".html" OrElse ext = ".htm" Then mime = "text/html; charset=UTF-8"
                If ext = ".css" Then mime = "text/css; charset=UTF-8"
                If ext = ".js" Then mime = "application/javascript; charset=UTF-8"
                If ext = ".json" Then mime = "application/json; charset=UTF-8"
                If ext = ".png" Then mime = "image/png"
                If ext = ".jpg" OrElse ext = ".jpeg" Then mime = "image/jpeg"
                If ext = ".svg" Then mime = "image/svg+xml"

                Dim bytes = File.ReadAllBytes(localFilePath)
                resp.ContentType = mime
                resp.ContentLength64 = bytes.Length
                resp.OutputStream.Write(bytes, 0, bytes.Length)
            ElseIf rawPath = "/" Or rawPath = "/index.html" Then
                Dim html = GetWebDashboardHtml()
                Dim buf = Encoding.UTF8.GetBytes(html)
                ctx.Response.ContentEncoding = Encoding.UTF8
                resp.ContentType = "text/html; charset=UTF-8"
                resp.ContentLength64 = buf.Length
                resp.OutputStream.Write(buf, 0, buf.Length)
            ElseIf rawPath = "/api/books" Then
                Dim json = GetBooksJson()
                Dim buf = Encoding.UTF8.GetBytes(json)
                ctx.Response.ContentEncoding = Encoding.UTF8
                resp.ContentType = "application/json; charset=UTF-8"
                resp.ContentLength64 = buf.Length
                resp.OutputStream.Write(buf, 0, buf.Length)
            ElseIf rawPath = "/api/stats" Then
                Dim libCount = 0
                Dim bookCount = 0
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim cmdL As New OleDbCommand("SELECT COUNT(*) FROM Libraries", conn)
                    libCount = Convert.ToInt32(cmdL.ExecuteScalar())
                    Dim cmdB As New OleDbCommand("SELECT COUNT(*) FROM Books", conn)
                    bookCount = Convert.ToInt32(cmdB.ExecuteScalar())
                End Using
                Dim json = String.Format("{{""libraries"":""{0}"",""books"":""{1}""}}", libCount, bookCount)
                Dim buf = Encoding.UTF8.GetBytes(json)
                resp.ContentType = "application/json"
                resp.OutputStream.Write(buf, 0, buf.Length)
            Else
                resp.StatusCode = 404
            End If
        Catch
            resp.StatusCode = 500
        Finally
            resp.Close()
        End Try
    End Sub

    Private Function GetBooksJson() As String
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT TOP 50 Title, Author, ISBN FROM Books WHERE LibraryName = ? ORDER BY ID DESC", conn)
                cmd.Parameters.AddWithValue("?", currentLibrary)
                Dim reader = cmd.ExecuteReader()
                Dim sb As New StringBuilder("[")
                While reader.Read()
                    sb.AppendFormat("{{""title"":""{0}"",""author"":""{1}"",""isbn"":""{2}""}},",
                                     reader("Title").ToString().Replace("""", "'"),
                                     reader("Author").ToString().Replace("""", "'"),
                                     reader("ISBN").ToString())
                End While
                If sb.Length > 1 Then sb.Length -= 1
                sb.Append("]")
                Return sb.ToString()
            End Using
        Catch
            Return "[]"
        End Try
    End Function

    Private Function GetWebDashboardHtml() As String
        Return "<!DOCTYPE html><html><head><meta charset='UTF-8'><title>My K Library Dashboard</title>" & _
        "<style>" & _
        "body { font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; background: #fafafa; margin: 0; padding: 24px; color: #333; }" & _
        ".container { max-width: 900px; margin: 0 auto; }" & _
        ".card { background: white; border-radius: 12px; padding: 24px; box-shadow: 0 4px 12px rgba(0,0,0,0.05); border: 1px solid #eaeaea; margin-bottom: 24px; }" & _
        "h1 { font-size: 2.2rem; margin: 0; color: #263238; font-weight: 700; }" & _
        ".subtitle { color: #78909c; margin: 6px 0 0 0; font-size: 0.95rem; }" & _
        ".search-bar { width: 100%; padding: 12px 16px; border-radius: 8px; border: 1px solid #cfd8dc; font-size: 1rem; margin: 16px 0; outline: none; transition: 0.2s; box-sizing: border-box; }" & _
        ".search-bar:focus { border-color: #2196f3; box-shadow: 0 0 0 3px rgba(33, 150, 243, 0.15); }" & _
        ".book-list { display: flex; flex-direction: column; gap: 12px; }" & _
        ".book-item { display: flex; align-items: center; padding: 16px; background: #fff; border: 1px solid #eceff1; border-radius: 8px; transition: transform 0.2s, box-shadow 0.2s; }" & _
        ".book-item:hover { transform: translateY(-2px); box-shadow: 0 4px 10px rgba(0,0,0,0.04); }" & _
        ".book-icon { font-size: 1.8rem; margin-right: 16px; }" & _
        ".book-info { flex: 1; }" & _
        ".book-title { font-weight: 600; color: #37474f; font-size: 1.1rem; margin-bottom: 4px; }" & _
        ".book-author { color: #78909c; font-size: 0.9rem; }" & _
        ".isbn-tag { font-size: 0.75rem; background: #eceff1; padding: 2px 6px; border-radius: 4px; color: #546e7a; margin-left: 8px; font-family: monospace; }" & _
        "</style></head><body>" & _
        "<div class='container'>" & _
        "  <div class='card'><h1>📂 My K Library</h1><p class='subtitle'>Minimalist Web Archive Dashboard (v2.0)</p></div>" & _
        "  <input type='text' id='search' class='search-bar' placeholder='Search by title, author or isbn...' onkeyup='filterBooks()'>" & _
        "  <div class='card'>" & _
        "    <h3 style='margin-top:0; color:#455a64;'>Books Collection</h3>" & _
        "    <div id='bookList' class='book-list'>Loading books...</div>" & _
        "  </div>" & _
        "</div>" & _
        "<script>" & _
        "let allBooks = [];" & _
        "async function load() {" & _
        "  try {" & _
        "    allBooks = await fetch('/api/books').then(r => r.json());" & _
        "    render(allBooks);" & _
        "  } catch(e) {" & _
        "    document.getElementById('bookList').innerHTML = '<span style=""color:red;"">Error loading collection.</span>';" & _
        "  }" & _
        "}" & _
        "function render(data) {" & _
        "  const list = document.getElementById('bookList');" & _
        "  if (data.length === 0) { list.innerHTML = '<i>No books in this library.</i>'; return; }" & _
        "  list.innerHTML = data.map(b => `" & _
        "    <div class='book-item'>" & _
        "      <div class='book-icon'>📕</div>" & _
        "      <div class='book-info'>" & _
        "        <div class='book-title'>${escapeHtml(b.title)} ${b.isbn ? `<span class='isbn-tag'>ISBN: ${escapeHtml(b.isbn)}</span>` : ''}</div>" & _
        "        <div class='book-author'>by ${escapeHtml(b.author || 'Unknown Author')}</div>" & _
        "      </div>" & _
        "    </div>`" & _
        "  ).join('');" & _
        "}" & _
        "function escapeHtml(str) {" & _
        "  if (!str) return '';" & _
        "  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"\"/g, '&quot;');" & _
        "}" & _
        "function filterBooks() {" & _
        "  const q = document.getElementById('search').value.toLowerCase();" & _
        "  const filtered = allBooks.filter(b => " & _
        "    (b.title && b.title.toLowerCase().includes(q)) || " & _
        "    (b.author && b.author.toLowerCase().includes(q)) || " & _
        "    (b.isbn && b.isbn.toLowerCase().includes(q))" & _
        "  );" & _
        "  render(filtered);" & _
        "}" & _
        "load();" & _
        "</script></body></html>"
    End Function
End Module

' ======================================================================
Public Module ThemeManager
    Public CurrentTheme As String = "Light Ivory"
    
    Public BackgroundColor As Color = Color.FromArgb(250, 250, 250)
    Public TextColor As Color = Color.FromArgb(38, 50, 56)
    Public PanelColor As Color = Color.FromArgb(245, 245, 245)
    Public AccentColor As Color = Color.FromArgb(33, 150, 243)
    Public GridHeaderColor As Color = Color.FromArgb(45, 52, 71)
    
    Public Sub SetTheme(themeName As String)
        CurrentTheme = themeName
        If themeName = "Dark Slate" Then
            BackgroundColor = Color.FromArgb(33, 33, 33)
            TextColor = Color.FromArgb(245, 245, 245)
            PanelColor = Color.FromArgb(42, 42, 42)
            AccentColor = Color.FromArgb(144, 202, 249)
            GridHeaderColor = Color.FromArgb(21, 21, 21)
        Else
            BackgroundColor = Color.FromArgb(250, 250, 250)
            TextColor = Color.FromArgb(38, 50, 56)
            PanelColor = Color.FromArgb(245, 245, 245)
            AccentColor = Color.FromArgb(30, 136, 229)
            GridHeaderColor = Color.FromArgb(45, 52, 71)
        End If
    End Sub
    
    Public Sub ApplyTheme(f As Form)
        f.BackColor = BackgroundColor
        f.ForeColor = TextColor
        For Each ctrl As Control In f.Controls
            ApplyThemeToControl(ctrl)
        Next
    End Sub
    
    Private Sub ApplyThemeToControl(c As Control)
        ' Exclude controls in the sidebar
        If c.Parent IsNot Nothing AndAlso (c.Parent.Name = "pnlSidebar" OrElse (c.Parent.Parent IsNot Nothing AndAlso c.Parent.Parent.Name = "pnlSidebar")) Then
            If TypeOf c Is Label Then
                If c.Name = "lblBrand" Then
                    c.ForeColor = Color.White
                ElseIf c.Name = "lblRole" Then
                    c.ForeColor = Color.FromArgb(176, 190, 197)
                Else
                    c.ForeColor = Color.FromArgb(207, 216, 220)
                End If
            End If
            ' Skip altering colors for panels and buttons in sidebar
            For Each child As Control In c.Controls
                ApplyThemeToControl(child)
            Next
            Return
        End If

        If TypeOf c Is Panel Then
            If c.Name = "pnlSidebar" Then
                c.BackColor = Color.FromArgb(34, 42, 53)
                c.ForeColor = Color.White
            ElseIf c.Name.StartsWith("card") Then
                c.BackColor = If(CurrentTheme = "Dark Slate", PanelColor, Color.White)
                c.ForeColor = TextColor
            Else
                c.BackColor = BackgroundColor
                c.ForeColor = TextColor
            End If
        ElseIf TypeOf c Is GroupBox Then
            c.ForeColor = TextColor
        ElseIf TypeOf c Is TabPage Then
            c.BackColor = BackgroundColor
            c.ForeColor = TextColor
        ElseIf TypeOf c Is Label Then
            If c.Name.EndsWith("Title") AndAlso c.Parent IsNot Nothing AndAlso c.Parent.Name.StartsWith("card") Then
                c.ForeColor = Color.Gray
            ElseIf c.Name.EndsWith("Val") AndAlso c.Parent IsNot Nothing AndAlso c.Parent.Name.StartsWith("card") Then
                c.ForeColor = AccentColor
            Else
                c.ForeColor = TextColor
            End If
        ElseIf TypeOf c Is TextBox Then
            If DirectCast(c, TextBox).ReadOnly Then
                c.BackColor = If(CurrentTheme = "Dark Slate", Color.FromArgb(50, 50, 50), Color.FromArgb(230, 230, 230))
            Else
                c.BackColor = If(CurrentTheme = "Dark Slate", Color.FromArgb(66, 66, 66), Color.White)
            End If
            c.ForeColor = TextColor
        ElseIf TypeOf c Is ComboBox Then
            c.BackColor = If(CurrentTheme = "Dark Slate", Color.FromArgb(66, 66, 66), Color.White)
            c.ForeColor = TextColor
        ElseIf TypeOf c Is ListBox Then
            c.BackColor = If(CurrentTheme = "Dark Slate", Color.FromArgb(66, 66, 66), Color.White)
            c.ForeColor = TextColor
        ElseIf TypeOf c Is DataGridView Then
            Dim dgv = DirectCast(c, DataGridView)
            dgv.BackgroundColor = BackgroundColor
            dgv.DefaultCellStyle.BackColor = If(CurrentTheme = "Dark Slate", Color.FromArgb(50, 50, 50), Color.White)
            dgv.DefaultCellStyle.ForeColor = TextColor
            dgv.GridColor = If(CurrentTheme = "Dark Slate", Color.FromArgb(66, 66, 66), Color.FromArgb(224, 224, 224))
            dgv.ColumnHeadersDefaultCellStyle.BackColor = GridHeaderColor
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
            dgv.AlternatingRowsDefaultCellStyle.BackColor = If(CurrentTheme = "Dark Slate", Color.FromArgb(40, 40, 40), Color.FromArgb(248, 249, 250))
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            dgv.DefaultCellStyle.SelectionBackColor = AccentColor
            dgv.DefaultCellStyle.SelectionForeColor = Color.White
        End If
        
        For Each child As Control In c.Controls
            ApplyThemeToControl(child)
        Next
    End Sub
End Module

' ======================================================================
' CLASS: LoginForm
' Description: Secure entry authentication page.
' ======================================================================
Public Class LoginForm
    Inherits Form
    Public UserName As String = ""
    Public UserRole As String = ""
    Private txtUser As TextBox
    Private txtPass As TextBox
    Private btnLogin As Button
    Private lblDbStatus As Label
    Private lblServerStatus As Label
    
    Public Sub New()
        Me.Text = "My K Library - Sign In"
        Me.Size = New Size(420, 395)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.BackColor = Color.White
        Me.Font = New Font("Segoe UI", 9.5F)
        
        Dim pnlTop As New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 70,
            .BackColor = Color.FromArgb(34, 42, 53)
        }
        Me.Controls.Add(pnlTop)
        
        Dim lblTitle As New Label() With {
            .Text = "📚 My K Library",
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
            .Location = New Point(18, 14),
            .AutoSize = True
        }
        pnlTop.Controls.Add(lblTitle)
        
        Dim lblSub As New Label() With {
            .Text = "Enterprise & Cloud Edition",
            .ForeColor = Color.FromArgb(176, 190, 197),
            .Font = New Font("Segoe UI", 8.5F),
            .Location = New Point(22, 40),
            .AutoSize = True
        }
        pnlTop.Controls.Add(lblSub)
        
        Dim lblUser As New Label() With {.Text = "Username", .Location = New Point(35, 84), .AutoSize = True, .ForeColor = Color.FromArgb(71, 85, 105), .Font = New Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)}
        Me.Controls.Add(lblUser)
        
        txtUser = New TextBox() With {.Location = New Point(35, 106), .Width = 335, .BorderStyle = BorderStyle.FixedSingle, .Font = New Font("Segoe UI", 10.5F)}
        Me.Controls.Add(txtUser)
        
        Dim lblPass As New Label() With {.Text = "Password", .Location = New Point(35, 142), .AutoSize = True, .ForeColor = Color.FromArgb(71, 85, 105), .Font = New Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)}
        Me.Controls.Add(lblPass)
        
        txtPass = New TextBox() With {.Location = New Point(35, 164), .Width = 335, .BorderStyle = BorderStyle.FixedSingle, .Font = New Font("Segoe UI", 10.5F), .UseSystemPasswordChar = True}
        Me.Controls.Add(txtPass)
        
        btnLogin = New Button() With {
            .Text = "Sign In to Library",
            .Location = New Point(35, 206),
            .Width = 335,
            .Height = 38,
            .BackColor = Color.FromArgb(37, 99, 235),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI Semibold", 10.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnLogin.FlatAppearance.BorderSize = 0
        AddHandler btnLogin.Click, AddressOf btnLogin_Click
        Me.Controls.Add(btnLogin)
        
        ' --- Backend Server State Panel ---
        Dim pnlState As New Panel() With {
            .Location = New Point(35, 256),
            .Width = 335,
            .Height = 82,
            .BackColor = Color.FromArgb(248, 250, 252),
            .BorderStyle = BorderStyle.FixedSingle
        }
        Me.Controls.Add(pnlState)
        
        Dim lblStateHeader As New Label() With {
            .Text = "BACKEND SERVER STATE",
            .Font = New Font("Segoe UI", 7.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(100, 116, 139),
            .Location = New Point(10, 6),
            .AutoSize = True
        }
        pnlState.Controls.Add(lblStateHeader)
        
        lblDbStatus = New Label() With {
            .Text = "Checking database connectivity...",
            .Font = New Font("Segoe UI", 8.0F),
            .ForeColor = Color.FromArgb(51, 65, 85),
            .Location = New Point(10, 26),
            .AutoSize = True
        }
        pnlState.Controls.Add(lblDbStatus)
        
        lblServerStatus = New Label() With {
            .Text = "Checking server status...",
            .Font = New Font("Segoe UI", 8.0F),
            .ForeColor = Color.FromArgb(51, 65, 85),
            .Location = New Point(10, 48),
            .AutoSize = True
        }
        pnlState.Controls.Add(lblServerStatus)
        
        Me.AcceptButton = btnLogin
        CheckBackendStatus()
        ThemeManager.ApplyTheme(Me)
    End Sub

    Private Sub CheckBackendStatus()
        ' 1. Check Database
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT COUNT(*) FROM Books", conn)
                Dim bCount = Convert.ToInt32(cmd.ExecuteScalar())
                lblDbStatus.Text = "🟢 Database: Connected (" & bCount & " books cataloged)"
                lblDbStatus.ForeColor = Color.FromArgb(22, 101, 52)
            End Using
        Catch ex As Exception
            lblDbStatus.Text = "🔴 Database: Connection Error (" & ex.Message & ")"
            lblDbStatus.ForeColor = Color.FromArgb(185, 28, 28)
        End Try
        
        ' 2. Live Check Supabase Cloud backend
        lblServerStatus.Text = "⏳ Cloud Backend: Checking Supabase..."
        lblServerStatus.ForeColor = Color.Gray
        Task.Run(Sub()
            Try
                Dim req = Net.WebRequest.Create("https://kgvopwljwpeqkaindqbs.supabase.co/rest/v1/books?select=id&limit=1")
                req.Headers.Add("apikey", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imtndm9wd2xqd3BlcWthaW5kcWJzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTI5NjYxMjAsImV4cCI6MjEwODU0MjEyMH0.M1MN2YSrZgCWC__9YOWVXRG34zxE8Wd7GrqnY6g1HHM")
                req.Timeout = 4000
                Dim sw = Stopwatch.StartNew()
                Using resp = req.GetResponse()
                    sw.Stop()
                    If Me.IsHandleCreated AndAlso Not Me.IsDisposed Then
                        Me.BeginInvoke(Sub()
                            lblServerStatus.Text = "🟢 Supabase Cloud: Online (" & sw.ElapsedMilliseconds & " ms)"
                            lblServerStatus.ForeColor = Color.FromArgb(22, 101, 52)
                        End Sub)
                    End If
                End Using
            Catch ex As Exception
                If Me.IsHandleCreated AndAlso Not Me.IsDisposed Then
                    Me.BeginInvoke(Sub()
                        lblServerStatus.Text = "🔴 Cloud Backend: Offline (" & ex.Message & ")"
                        lblServerStatus.ForeColor = Color.FromArgb(185, 28, 28)
                    End Sub)
                End If
            End Try
        End Sub)
    End Sub
    
    Private Sub btnLogin_Click(sender As Object, e As EventArgs)
        Dim u = txtUser.Text.Trim()
        Dim p = txtPass.Text.Trim()
        
        If String.IsNullOrEmpty(u) Then Return
        
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT [Role] FROM [Users] WHERE [Username] = ? AND [Password] = ?", conn)
                cmd.Parameters.AddWithValue("?", u)
                cmd.Parameters.AddWithValue("?", p)
                Dim res = cmd.ExecuteScalar()
                If res IsNot Nothing Then
                    UserName = u
                    UserRole = res.ToString()
                    Me.DialogResult = DialogResult.OK
                    Me.Close()
                Else
                    MessageBox.Show("Invalid credentials. Hint: admin / 2026", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show("Login database check failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class

' ======================================================================
' CLASS: ProgressForm
' Description: Simple helper dialog to track progress visually.
' ======================================================================
Public Class ProgressForm
    Inherits Form
    Public lblStatus As Label
    Public pb As ProgressBar
    
    Public Sub New(title As String)
        Me.Text = title
        Me.Size = New Size(400, 130)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.ControlBox = False
        Me.BackColor = Color.White
        
        lblStatus = New Label() With {.Text = "Starting...", .Location = New Point(20, 20), .Size = New Size(360, 25)}
        pb = New ProgressBar() With {.Location = New Point(20, 50), .Size = New Size(340, 20), .Style = ProgressBarStyle.Blocks}
        
        Me.Controls.Add(lblStatus)
        Me.Controls.Add(pb)
    End Sub
End Class

' ======================================================================
' CLASS: MainForm
' Description: Main window with library selector, toolbar, search, and books grid.
' ======================================================================
Public Class MainForm
    Inherits Form
    
    ' Layout containers
    Private pnlSidebar As Panel
    Private pnlContent As Panel
    Private pnlHeader As Panel
    Private pnlStats As Panel
    Private pnlGridActions As Panel
    Private pnlGrid As Panel
    
    ' Sidebar controls
    Private btnSidebarLibraries As Button
    Private btnSidebarSettings As Button
    Private btnSidebarLock As Button
    Private btnSidebarTouch As Button
    Private btnSidebarLogout As Button
    
    ' Toolbar / search controls
    Private cmbLibraries As ComboBox
    Private txtSearch As TextBox
    
    ' Stats card values
    Private lblCardBooksVal As Label
    Private lblCardCatVal As Label
    Private lblCardServVal As Label
    
    ' Header labels
    Private lblLibraryTitle As Label
    Private lblSubTitle As Label
    
    ' Grid controls
    Private dgvBooks As DataGridView
    Private bsBooks As BindingSource
    
    ' Grid actions
    Private btnAdd As Button
    Private btnEdit As Button
    Private btnDelete As Button
    Private btnReadOnline As Button
    
    Public Sub New()
        InitializeComponent()
        LoadWindowSize()
        LoadLibraries()
        UpdateLockStatus()
        ApplyTouchMode()
        ThemeManager.ApplyTheme(Me)
    End Sub
    
    Private Sub InitializeComponent()
        Me.Text = "My K Library - 2.0.0"
        Me.Size = New Size(1000, 650)
        Me.MinimumSize = New Size(850, 550)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Font = New Font("Segoe UI", Program.BaseFontSize)
        Me.DoubleBuffered = True
        
        bsBooks = New BindingSource()
        
        ' 1. Sidebar Panel (docked Left)
        pnlSidebar = New Panel() With {
            .Name = "pnlSidebar",
            .Dock = DockStyle.Left,
            .Width = 240,
            .BackColor = Color.FromArgb(34, 42, 53)
        }
        Me.Controls.Add(pnlSidebar)
        
        ' Brand / logo
        Dim lblBrand As New Label() With {
            .Name = "lblBrand",
            .Text = "📚 K-LIBRARY v2.0",
            .Font = New Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
            .ForeColor = Color.White,
            .Location = New Point(18, 25),
            .AutoSize = True
        }
        pnlSidebar.Controls.Add(lblBrand)
        
        Dim lblRole As New Label() With {
            .Name = "lblRole",
            .Text = "Role: " & Program.SessionRole,
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Italic),
            .ForeColor = Color.FromArgb(176, 190, 197),
            .Location = New Point(18, 52),
            .AutoSize = True
        }
        pnlSidebar.Controls.Add(lblRole)
        
        Dim pnlSep As New Panel() With {
            .Size = New Size(204, 1),
            .Location = New Point(18, 75),
            .BackColor = Color.FromArgb(55, 71, 79)
        }
        pnlSidebar.Controls.Add(pnlSep)
        
        ' Navigation buttons in sidebar
        btnSidebarLibraries = New Button() With {
            .Text = "   📂 Library Manager",
            .Location = New Point(10, 95),
            .Width = 220,
            .Height = 40,
            .FlatStyle = FlatStyle.Flat,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9.5F),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Cursor = Cursors.Hand
        }
        btnSidebarLibraries.FlatAppearance.BorderSize = 0
        btnSidebarLibraries.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 58, 73)
        AddHandler btnSidebarLibraries.Click, AddressOf btnSidebarLibraries_Click
        pnlSidebar.Controls.Add(btnSidebarLibraries)
        
        btnSidebarSettings = New Button() With {
            .Text = "   ⚙ Settings",
            .Location = New Point(10, 140),
            .Width = 220,
            .Height = 40,
            .FlatStyle = FlatStyle.Flat,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9.5F),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Cursor = Cursors.Hand
        }
        btnSidebarSettings.FlatAppearance.BorderSize = 0
        btnSidebarSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 58, 73)
        AddHandler btnSidebarSettings.Click, AddressOf btnSidebarSettings_Click
        pnlSidebar.Controls.Add(btnSidebarSettings)
        
        btnSidebarLock = New Button() With {
            .Location = New Point(10, 185),
            .Width = 220,
            .Height = 40,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Cursor = Cursors.Hand
        }
        btnSidebarLock.FlatAppearance.BorderSize = 0
        btnSidebarLock.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 58, 73)
        AddHandler btnSidebarLock.Click, AddressOf btnSidebarLock_Click
        pnlSidebar.Controls.Add(btnSidebarLock)
        
        btnSidebarTouch = New Button() With {
            .Text = "   🖐️ Touch Mode",
            .Location = New Point(10, 230),
            .Width = 220,
            .Height = 40,
            .FlatStyle = FlatStyle.Flat,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9.5F),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Cursor = Cursors.Hand
        }
        btnSidebarTouch.FlatAppearance.BorderSize = 0
        btnSidebarTouch.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 58, 73)
        AddHandler btnSidebarTouch.Click, AddressOf btnSidebarTouch_Click
        pnlSidebar.Controls.Add(btnSidebarTouch)
        
        btnSidebarLogout = New Button() With {
            .Text = "   🚪 Sign Out",
            .Location = New Point(10, 275),
            .Width = 220,
            .Height = 40,
            .FlatStyle = FlatStyle.Flat,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9.5F),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Cursor = Cursors.Hand
        }
        btnSidebarLogout.FlatAppearance.BorderSize = 0
        btnSidebarLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 58, 73)
        AddHandler btnSidebarLogout.Click, AddressOf btnSidebarLogout_Click
        pnlSidebar.Controls.Add(btnSidebarLogout)
        
        ' User status at bottom of sidebar
        Dim pnlSidebarStatus As New Panel() With {
            .Dock = DockStyle.Bottom,
            .Height = 60,
            .BackColor = Color.Transparent
        }
        pnlSidebar.Controls.Add(pnlSidebarStatus)
        
        Dim lblStatusUser As New Label() With {
            .Text = "User: " & Program.SessionUser,
            .Font = New Font("Segoe UI", 8.0F),
            .ForeColor = Color.FromArgb(144, 164, 174),
            .Location = New Point(18, 12),
            .AutoSize = True
        }
        pnlSidebarStatus.Controls.Add(lblStatusUser)
        
        Dim lblStatusRole As New Label() With {
            .Text = "Status: Online",
            .Font = New Font("Segoe UI", 8.0F),
            .ForeColor = Color.FromArgb(76, 175, 80),
            .Location = New Point(18, 30),
            .AutoSize = True
        }
        pnlSidebarStatus.Controls.Add(lblStatusRole)
        
        ' 2. Main Content Panel (docked Fill)
        pnlContent = New Panel() With {
            .Name = "pnlContent",
            .Dock = DockStyle.Fill
        }
        Me.Controls.Add(pnlContent)
        
        ' Ensure proper docking order (pnlSidebar docks first, pnlContent fills the remaining space)
        pnlSidebar.SendToBack()
        pnlContent.BringToFront()
        
        ' 3. Header Panel (docked Top inside Content)
        pnlHeader = New Panel() With {
            .Name = "pnlHeader",
            .Dock = DockStyle.Top,
            .Height = 85
        }
        pnlContent.Controls.Add(pnlHeader)
        
        lblLibraryTitle = New Label() With {
            .Name = "lblLibraryTitle",
            .Text = Program.currentLibrary,
            .Font = New Font("Segoe UI Semibold", 15.0F, FontStyle.Bold),
            .Location = New Point(25, 20),
            .AutoSize = True
        }
        pnlHeader.Controls.Add(lblLibraryTitle)
        
        lblSubTitle = New Label() With {
            .Name = "lblSubTitle",
            .Text = "Manage books, update metadata, and harvest covers",
            .Font = New Font("Segoe UI", 8.5F),
            .ForeColor = Color.Gray,
            .Location = New Point(25, 48),
            .AutoSize = True
        }
        pnlHeader.Controls.Add(lblSubTitle)
        
        ' Search and selector controls arranged on the right of header using a right-docked panel
        Dim pnlHeaderRight As New Panel() With {
            .Name = "pnlHeaderRight",
            .Dock = DockStyle.Right,
            .Width = 380,
            .BackColor = Color.Transparent
        }
        pnlHeader.Controls.Add(pnlHeaderRight)
        
        Dim lblSelectLib As New Label() With {
            .Text = "Library:",
            .Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold),
            .Location = New Point(10, 32),
            .AutoSize = True
        }
        pnlHeaderRight.Controls.Add(lblSelectLib)
        
        cmbLibraries = New ComboBox() With {
            .Location = New Point(65, 28),
            .Width = 110,
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        AddHandler cmbLibraries.SelectedIndexChanged, AddressOf cmbLibraries_SelectedIndexChanged
        pnlHeaderRight.Controls.Add(cmbLibraries)
        
        Dim lblSearch As New Label() With {
            .Text = "Search:",
            .Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold),
            .Location = New Point(190, 32),
            .AutoSize = True
        }
        pnlHeaderRight.Controls.Add(lblSearch)
        
        txtSearch = New TextBox() With {
            .Location = New Point(245, 28),
            .Width = 120,
            .BorderStyle = BorderStyle.FixedSingle,
            .Font = New Font("Segoe UI", 9.5F)
        }
        AddHandler txtSearch.TextChanged, AddressOf txtSearch_TextChanged
        pnlHeaderRight.Controls.Add(txtSearch)
        
        ' 4. Stats Panel (docked Top)
        pnlStats = New Panel() With {
            .Name = "pnlStats",
            .Dock = DockStyle.Top,
            .Height = 75
        }
        pnlContent.Controls.Add(pnlStats)
        
        ' Card 1: Books
        Dim cardBooks As New Panel() With {
            .Name = "cardBooks",
            .Size = New Size(160, 58),
            .Location = New Point(25, 8),
            .BorderStyle = BorderStyle.None
        }
        pnlStats.Controls.Add(cardBooks)
        Dim lblCardBooksTitle As New Label() With {
            .Name = "lblCardBooksTitle",
            .Text = "TOTAL BOOKS",
            .Font = New Font("Segoe UI", 7.5F, FontStyle.Bold),
            .Location = New Point(12, 10),
            .AutoSize = True
        }
        cardBooks.Controls.Add(lblCardBooksTitle)
        lblCardBooksVal = New Label() With {
            .Name = "lblCardBooksVal",
            .Text = "0",
            .Font = New Font("Segoe UI Semibold", 13.0F, FontStyle.Bold),
            .Location = New Point(12, 26),
            .AutoSize = True
        }
        cardBooks.Controls.Add(lblCardBooksVal)
        
        ' Card 2: Categories
        Dim cardCategories As New Panel() With {
            .Name = "cardCategories",
            .Size = New Size(160, 58),
            .Location = New Point(200, 8),
            .BorderStyle = BorderStyle.None
        }
        pnlStats.Controls.Add(cardCategories)
        Dim lblCardCatTitle As New Label() With {
            .Name = "lblCardCatTitle",
            .Text = "CATEGORIES",
            .Font = New Font("Segoe UI", 7.5F, FontStyle.Bold),
            .Location = New Point(12, 10),
            .AutoSize = True
        }
        cardCategories.Controls.Add(lblCardCatTitle)
        lblCardCatVal = New Label() With {
            .Name = "lblCardCatVal",
            .Text = "0",
            .Font = New Font("Segoe UI Semibold", 13.0F, FontStyle.Bold),
            .Location = New Point(12, 26),
            .AutoSize = True
        }
        cardCategories.Controls.Add(lblCardCatVal)
        
        ' Card 3: Dashboard Server
        Dim cardServer As New Panel() With {
            .Name = "cardServer",
            .Size = New Size(200, 58),
            .Location = New Point(375, 8),
            .BorderStyle = BorderStyle.None
        }
        pnlStats.Controls.Add(cardServer)
        Dim lblCardServTitle As New Label() With {
            .Name = "lblCardServTitle",
            .Text = "CLOUD BACKEND",
            .Font = New Font("Segoe UI", 7.5F, FontStyle.Bold),
            .Location = New Point(12, 10),
            .AutoSize = True
        }
        cardServer.Controls.Add(lblCardServTitle)
        lblCardServVal = New Label() With {
            .Name = "lblCardServVal",
            .Text = "Checking...",
            .Font = New Font("Segoe UI Semibold", 10.0F, FontStyle.Bold),
            .Location = New Point(12, 26),
            .AutoSize = True
        }
        cardServer.Controls.Add(lblCardServVal)
        
        ' 5. Grid Actions Toolbar (docked Top)
        pnlGridActions = New Panel() With {
            .Name = "pnlGridActions",
            .Dock = DockStyle.Top,
            .Height = 45
        }
        pnlContent.Controls.Add(pnlGridActions)
        
        btnAdd = New Button() With {
            .Text = "+ Add Book",
            .Location = New Point(25, 8),
            .Width = 110,
            .Height = 30,
            .BackColor = Color.FromArgb(46, 125, 50),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnAdd.FlatAppearance.BorderSize = 0
        AddHandler btnAdd.Click, AddressOf btnAdd_Click
        pnlGridActions.Controls.Add(btnAdd)
        
        btnEdit = New Button() With {
            .Text = "✎ Edit Selected",
            .Location = New Point(145, 8),
            .Width = 120,
            .Height = 30,
            .BackColor = Color.FromArgb(30, 136, 229),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnEdit.FlatAppearance.BorderSize = 0
        AddHandler btnEdit.Click, AddressOf btnEdit_Click
        pnlGridActions.Controls.Add(btnEdit)
        
        btnDelete = New Button() With {
            .Text = "✖ Delete",
            .Location = New Point(275, 8),
            .Width = 90,
            .Height = 30,
            .BackColor = Color.FromArgb(211, 47, 47),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnDelete.FlatAppearance.BorderSize = 0
        AddHandler btnDelete.Click, AddressOf btnDelete_Click
        pnlGridActions.Controls.Add(btnDelete)

        btnReadOnline = New Button() With {
            .Text = "📖 Read Online",
            .Location = New Point(375, 8),
            .Width = 125,
            .Height = 30,
            .BackColor = Color.FromArgb(16, 185, 129),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnReadOnline.FlatAppearance.BorderSize = 0
        AddHandler btnReadOnline.Click, AddressOf btnReadOnline_Click
        pnlGridActions.Controls.Add(btnReadOnline)
        
        ' 6. Grid Container Panel (docked Fill)
        pnlGrid = New Panel() With {
            .Name = "pnlGrid",
            .Dock = DockStyle.Fill,
            .Padding = New Padding(25, 5, 25, 25)
        }
        pnlContent.Controls.Add(pnlGrid)
        
        ' Ensure proper docking order inside pnlContent
        pnlHeader.BringToFront()
        pnlStats.BringToFront()
        pnlGridActions.BringToFront()
        pnlGrid.BringToFront()
        
        ' DataGridView
        dgvBooks = New DataGridView() With {
            .Dock = DockStyle.Fill,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .ReadOnly = True,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .MultiSelect = False,
            .BackgroundColor = Color.White,
            .BorderStyle = BorderStyle.None,
            .CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            .RowHeadersVisible = False,
            .EnableHeadersVisualStyles = False,
            .AllowUserToResizeRows = False
        }
        dgvBooks.RowTemplate.Height = 30
        dgvBooks.ColumnHeadersHeight = 32
        
        ' Double buffer for smooth scrolling
        Try
            Dim dgvType = dgvBooks.GetType()
            Dim pi = dgvType.GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance Or System.Reflection.BindingFlags.NonPublic)
            If pi IsNot Nothing Then
                pi.SetValue(dgvBooks, True, Nothing)
            End If
        Catch
        End Try
        
        AddHandler dgvBooks.CellDoubleClick, AddressOf dgvBooks_CellDoubleClick
        pnlGrid.Controls.Add(dgvBooks)
        
        AddHandler Me.FormClosing, AddressOf MainForm_FormClosing
    End Sub
    
    Private Sub LoadLibraries()
        cmbLibraries.Items.Clear()
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT LibName FROM Libraries ORDER BY LibName", conn)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        cmbLibraries.Items.Add(reader("LibName").ToString())
                    End While
                End Using
            End Using
            If cmbLibraries.Items.Count > 0 Then
                cmbLibraries.SelectedItem = Program.currentLibrary
                If cmbLibraries.SelectedIndex = -1 Then cmbLibraries.SelectedIndex = 0
            End If
        Catch ex As Exception
            MessageBox.Show("Error loading libraries: " & ex.Message)
        End Try
    End Sub
    
    Private Sub cmbLibraries_SelectedIndexChanged(sender As Object, e As EventArgs)
        If cmbLibraries.SelectedItem IsNot Nothing Then
            Program.currentLibrary = cmbLibraries.SelectedItem.ToString()
            lblLibraryTitle.Text = Program.currentLibrary
            LoadBooks()
        End If
    End Sub
    
    Public Sub LoadBooks()
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand(
                    "SELECT B.[ID], B.[Title], B.[Author], B.[Author2], B.[Author3], B.[Author4], B.[Author5], B.[Author6], B.[Author7], B.[Author8], B.[Author9], B.[Author10], B.[CoAuthors], B.[Pages], B.[PubYear], B.[Publisher], B.[ISBN], B.[CopyNum], B.[Series], B.[CoverPath], B.[BackPath], B.[Description], B.[Language], B.[Weight], B.[Dimensions], B.[LCCN], B.[OCLC], B.[Subjects], B.[AddedBy], B.[EditedBy], B.[ReadingUrl], B.[IsEBook], C.CategoryName AS [Book Category], B.CategoryID " &
                    "FROM Books AS B LEFT JOIN BookCategories AS C ON B.CategoryID = C.CatID " &
                    "WHERE B.LibraryName = ? ORDER BY B.ID DESC", conn)
                cmd.Parameters.AddWithValue("?", Program.currentLibrary)
                
                Dim adapter As New OleDbDataAdapter(cmd)
                Dim dt As New DataTable()
                adapter.Fill(dt)
                bsBooks.DataSource = dt
                dgvBooks.DataSource = bsBooks
            End Using
            
            ' Apply some formatting to the DataGridView
            dgvBooks.Columns("CategoryID").Visible = False
            dgvBooks.Columns("CoverPath").Visible = False
            dgvBooks.Columns("BackPath").Visible = False
            dgvBooks.Columns("Description").Visible = False
            dgvBooks.Columns("AddedBy").Visible = False
            dgvBooks.Columns("EditedBy").Visible = False
            dgvBooks.Columns("Subjects").Visible = False
            dgvBooks.Columns("LCCN").Visible = False
            dgvBooks.Columns("OCLC").Visible = False
            dgvBooks.Columns("Weight").Visible = False
            dgvBooks.Columns("Dimensions").Visible = False
            dgvBooks.Columns("Language").Visible = False
            If dgvBooks.Columns.Contains("ReadingUrl") Then dgvBooks.Columns("ReadingUrl").Visible = False
            If dgvBooks.Columns.Contains("IsEBook") Then dgvBooks.Columns("IsEBook").Visible = False
            
            ' Hide the extra author fields to keep the grid extremely clean
            For i As Integer = 2 To 10
                dgvBooks.Columns("Author" & i).Visible = False
            Next
            dgvBooks.Columns("CoAuthors").Visible = False
            dgvBooks.Columns("CopyNum").Visible = False
            dgvBooks.Columns("Series").Visible = False
            dgvBooks.Columns("Publisher").Visible = False
            
            ' Auto-resize key columns to fill space beautifully by default
            dgvBooks.Columns("Title").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            dgvBooks.Columns("Author").Width = 150
            dgvBooks.Columns("Book Category").Width = 120
            dgvBooks.Columns("ISBN").Width = 120
            
            ' Ensure grid layout file settings are loaded if any (overrides defaults)
            LoadGridLayout()
            
            For Each row As DataGridViewRow In dgvBooks.Rows
                row.Height = If(Program.TouchMode, 50, 30)
            Next
            
            ' Update Stats Cards
            Dim totalBooksCount = dgvBooks.Rows.Count
            lblCardBooksVal.Text = totalBooksCount.ToString()
            
            Dim dtBooks = CType(bsBooks.DataSource, DataTable)
            Dim cats As New HashSet(Of String)()
            For Each row As DataRow In dtBooks.Rows
                If Not row.IsNull("Book Category") Then
                    Dim catName = row("Book Category").ToString().Trim()
                    If catName <> "" Then cats.Add(catName)
                End If
            Next
            lblCardCatVal.Text = cats.Count.ToString()
            
            If Program.listener IsNot Nothing AndAlso Program.listener.IsListening Then
                lblCardServVal.Text = "Active / Cloud Synced"
                lblCardServVal.ForeColor = Color.FromArgb(76, 175, 80)
            Else
                lblCardServVal.Text = "Supabase Online"
                lblCardServVal.ForeColor = Color.FromArgb(37, 99, 235)
            End If
            
        Catch ex As Exception
            MessageBox.Show("Error loading books: " & ex.Message)
        End Try
    End Sub
    
    Private Sub txtSearch_TextChanged(sender As Object, e As EventArgs)
        Dim filterText As String = txtSearch.Text.Trim().Replace("'", "''")
        If String.IsNullOrEmpty(filterText) Then
            bsBooks.Filter = ""
        Else
            bsBooks.Filter = String.Format("[Title] LIKE '%{0}%' OR [Author] LIKE '%{0}%' OR [ISBN] LIKE '%{0}%'", filterText)
        End If
        
        ' Re-update books count dynamically
        lblCardBooksVal.Text = dgvBooks.Rows.Count.ToString()
    End Sub
    
    Private Sub btnAdd_Click(sender As Object, e As EventArgs)
        If Program.IsLocked Then
            MessageBox.Show("Please unlock database editing first.", "Database Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Using editor As New BookEditorForm(0)
            If editor.ShowDialog() = DialogResult.OK Then
                LoadBooks()
            End If
        End Using
    End Sub
    
    Private Sub btnEdit_Click(sender As Object, e As EventArgs)
        If dgvBooks.SelectedRows.Count = 0 Then
            MessageBox.Show("Please select a book first.", "Selection Required")
            Return
        End If
        Dim bookId As Integer = Convert.ToInt32(dgvBooks.SelectedRows(0).Cells("ID").Value)
        Using editor As New BookEditorForm(bookId)
            If editor.ShowDialog() = DialogResult.OK Then
                LoadBooks()
            End If
        End Using
    End Sub
    
    Private Sub dgvBooks_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 Then
            Dim bookId As Integer = Convert.ToInt32(dgvBooks.Rows(e.RowIndex).Cells("ID").Value)
            Using editor As New BookEditorForm(bookId)
                If editor.ShowDialog() = DialogResult.OK Then
                    LoadBooks()
                End If
            End Using
        End If
    End Sub
    
    
    Private Sub btnReadOnline_Click(sender As Object, e As EventArgs)
        If dgvBooks.SelectedRows.Count = 0 Then
            MessageBox.Show("Please select an e-book from the list first.", "Read Online", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            Dim row = CType(dgvBooks.SelectedRows(0).DataBoundItem, DataRowView).Row
            Dim url As String = ""
            If row.Table.Columns.Contains("ReadingUrl") AndAlso Not row.IsNull("ReadingUrl") Then
                url = row("ReadingUrl").ToString().Trim()
            End If
            If String.IsNullOrEmpty(url) Then
                Dim title = row("Title").ToString()
                Dim author = If(row.IsNull("Author"), "", row("Author").ToString())
                url = "https://openlibrary.org/search?q=" & Uri.EscapeDataString(title & " " & author)
            End If
            Process.Start(url)
        Catch ex As Exception
            MessageBox.Show("Could not open reading link: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnDelete_Click(sender As Object, e As EventArgs)
        If Program.IsLocked Then
            MessageBox.Show("Please unlock database editing first.", "Database Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If dgvBooks.SelectedRows.Count = 0 Then
            MessageBox.Show("Please select a book first.", "Selection Required")
            Return
        End If
        Dim bookId As Integer = Convert.ToInt32(dgvBooks.SelectedRows(0).Cells("ID").Value)
        Dim title As String = dgvBooks.SelectedRows(0).Cells("Title").Value.ToString()
        If MessageBox.Show(String.Format("Are you sure you want to delete '{0}'?", title), "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            Try
                Using conn As New OleDbConnection(Program.connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("DELETE FROM Books WHERE ID = ?", conn)
                    cmd.Parameters.AddWithValue("?", bookId)
                    cmd.ExecuteNonQuery()
                End Using
                LoadBooks()
            Catch ex As Exception
                MessageBox.Show("Error deleting book: " & ex.Message)
            End Try
        End If
    End Sub
    
    Private Sub btnSidebarLibraries_Click(sender As Object, e As EventArgs)
        Using manager As New LibraryManagerForm()
            manager.ShowDialog()
            LoadLibraries()
        End Using
    End Sub
    
    Private Sub btnSidebarSettings_Click(sender As Object, e As EventArgs)
        Using settingsDlg As New SettingsForm()
            settingsDlg.ShowDialog()
            ThemeManager.ApplyTheme(Me)
            Me.Font = New Font("Segoe UI", Program.BaseFontSize)
            LoadLibraries()
            LoadBooks()
        End Using
    End Sub
    
    Private Sub btnSidebarLock_Click(sender As Object, e As EventArgs)
        Program.IsLocked = Not Program.IsLocked
        UpdateLockStatus()
        If Program.IsLocked Then
            MessageBox.Show("Database editing locked.", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            MessageBox.Show("Database editing unlocked.", "Unlocked", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub btnSidebarTouch_Click(sender As Object, e As EventArgs)
        Program.TouchMode = Not Program.TouchMode
        Program.SaveSetting("TouchMode", If(Program.TouchMode, "True", "False"))
        ApplyTouchMode()
        If Program.TouchMode Then
            MessageBox.Show("Touch Screen UI enabled! Rows, action buttons, and touch targets are now enlarged for finger touch.", "Touch Mode ON", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            MessageBox.Show("Mouse / Compact UI restored.", "Touch Mode OFF", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub ApplyTouchMode()
        If btnSidebarTouch Is Nothing Then Return
        If Program.TouchMode Then
            btnSidebarTouch.Text = "  🖐️ Touch UI: ON"
            btnSidebarTouch.BackColor = Color.FromArgb(2, 119, 189)
            btnSidebarTouch.ForeColor = Color.White
            pnlGridActions.Height = 56
            btnAdd.Height = 42
            btnAdd.Width = 135
            btnAdd.Font = New Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
            btnEdit.Height = 42
            btnEdit.Width = 145
            btnEdit.Font = New Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
            btnEdit.Location = New Point(170, 7)
            btnDelete.Height = 42
            btnDelete.Width = 110
            btnDelete.Font = New Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
            btnDelete.Location = New Point(315, 7)
            btnReadOnline.Height = 42
            btnReadOnline.Width = 150
            btnReadOnline.Font = New Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
            btnReadOnline.Location = New Point(435, 7)

            dgvBooks.RowTemplate.Height = 50
            dgvBooks.ColumnHeadersHeight = 44
            dgvBooks.DefaultCellStyle.Font = New Font("Segoe UI", 11.5F)
            dgvBooks.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
            For Each row As DataGridViewRow In dgvBooks.Rows
                row.Height = 50
            Next
        Else
            btnSidebarTouch.Text = "  🖐️ Touch UI: OFF"
            btnSidebarTouch.BackColor = Color.FromArgb(48, 58, 73)
            btnSidebarTouch.ForeColor = Color.White
            pnlGridActions.Height = 45
            btnAdd.Height = 30
            btnAdd.Width = 110
            btnAdd.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold)
            btnEdit.Height = 30
            btnEdit.Width = 120
            btnEdit.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold)
            btnEdit.Location = New Point(145, 8)
            btnDelete.Height = 30
            btnDelete.Width = 90
            btnDelete.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold)
            btnDelete.Location = New Point(275, 8)
            btnReadOnline.Height = 30
            btnReadOnline.Width = 125
            btnReadOnline.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold)
            btnReadOnline.Location = New Point(375, 8)

            dgvBooks.RowTemplate.Height = 30
            dgvBooks.ColumnHeadersHeight = 32
            dgvBooks.DefaultCellStyle.Font = New Font("Segoe UI", 9.5F)
            dgvBooks.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold)
            For Each row As DataGridViewRow In dgvBooks.Rows
                row.Height = 30
            Next
        End If
    End Sub
    
    Private Sub btnSidebarLogout_Click(sender As Object, e As EventArgs)
        If MessageBox.Show("Are you sure you want to sign out?", "Confirm Sign Out", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            Program.LogoutRequested = True
            Me.Close()
        End If
    End Sub
    
    Private Sub UpdateLockStatus()
        If Program.IsLocked Then
            btnSidebarLock.Text = "  🔒 Locked"
            btnSidebarLock.BackColor = Color.FromArgb(198, 40, 40)
            btnSidebarLock.ForeColor = Color.White
        Else
            btnSidebarLock.Text = "  🔓 Unlocked"
            btnSidebarLock.BackColor = Color.FromArgb(46, 125, 50)
            btnSidebarLock.ForeColor = Color.White
        End If
    End Sub
    
    Private Sub SaveGridLayout()
        Try
            Dim orderData As New List(Of String)()
            For Each col As DataGridViewColumn In dgvBooks.Columns
                orderData.Add(col.Name & ":" & col.DisplayIndex & ":" & col.Width & ":" & col.Visible)
            Next
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grid_layout.txt"), String.Join("|", orderData.ToArray()))
            
            Dim w As Integer = Me.Width
            Dim h As Integer = Me.Height
            If Me.WindowState = FormWindowState.Minimized OrElse Me.WindowState = FormWindowState.Maximized Then
                w = Me.RestoreBounds.Width
                h = Me.RestoreBounds.Height
            End If
            If w >= 800 AndAlso h >= 500 Then
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_size.txt"), w & "," & h)
            End If
        Catch
        End Try
    End Sub

    Private Sub LoadWindowSize()
        Try
            Dim sizeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_size.txt")
            If File.Exists(sizeFile) Then
                Dim sz = File.ReadAllText(sizeFile).Split(","c)
                If sz.Length = 2 Then
                    Dim w = CInt(sz(0))
                    Dim h = CInt(sz(1))
                    If w >= 800 AndAlso h >= 500 Then
                        Me.Size = New Size(w, h)
                    Else
                        Me.Size = New Size(1000, 650)
                    End If
                End If
            End If
        Catch
        End Try
    End Sub

    Private Sub LoadGridLayout()
        Try
            Dim layoutFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grid_layout.txt")
            If File.Exists(layoutFile) Then
                Dim data = File.ReadAllText(layoutFile).Split("|"c)
                For Each item In data
                    Dim parts = item.Split(":"c)
                    If parts.Length = 4 AndAlso dgvBooks.Columns.Contains(parts(0)) Then
                        Dim col = dgvBooks.Columns(parts(0))
                        col.DisplayIndex = CInt(parts(1))
                        col.Width = CInt(parts(2))
                        col.Visible = CBool(parts(3))
                    End If
                Next
            End If
        Catch
        End Try
    End Sub
    
    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        SaveGridLayout()
    End Sub
End Class

' ======================================================================
' CLASS: CategoryItem
' Description: Key-value pair container for ComboBox.
' ======================================================================
Public Class CategoryItem
    Public Property CatID As Integer
    Public Property CategoryName As String
    Public Sub New(id As Integer, name As String)
        Me.CatID = id
        Me.CategoryName = name
    End Sub
    Public Overrides Function ToString() As String
        Return CategoryName
    End Function
End Class

' ======================================================================
' CLASS: BookEditorForm
' Description: Large, tabbed popup dialog to add/edit books.
' ======================================================================
Public Class BookEditorForm
    Inherits Form
    Private _bookId As Integer
    Private tabCtrl As TabControl
    Private tpBasic As TabPage
    Private tpAuthors As TabPage
    Private tpTech As TabPage
    Private tpDescCovers As TabPage
    Private tpLookup As TabPage
    
    ' Basic info controls
    Private txtTitle As TextBox
    Private txtAuthor As TextBox
    Private cmbCategory As ComboBox
    Private txtPublisher As TextBox
    Private txtPubYear As TextBox
    Private txtPages As TextBox
    Private txtISBN As TextBox
    Private txtSeries As TextBox
    Private txtCopyNum As TextBox
    
    ' Co-Authors controls
    Private txtCoAuthors As TextBox
    Private txtAuthors2To10(8) As TextBox
    
    ' Technical details
    Private txtLanguage As TextBox
    Private txtWeight As TextBox
    Private txtDimensions As TextBox
    Private txtLCCN As TextBox
    Private txtOCLC As TextBox
    Private txtSubjects As TextBox
    
    ' Description & Covers
    Private txtDescription As TextBox
    Private txtCoverPath As TextBox
    Private txtBackPath As TextBox
    Private pbCover As PictureBox
    Private pbBack As PictureBox
    Private btnBrowseCover As Button
    Private btnBrowseBack As Button
    
    ' Lookup controls
    Private txtScannerInput As TextBox
    Private btnGoogleLookup As Button
    Private btnGeminiLookup As Button
    Private lblLookupStatus As Label
    
    ' Footer buttons
    Private btnSave As Button
    Private btnCancel As Button
    Private lblFormStatus As Label

    Public Sub New(bookId As Integer)
        _bookId = bookId
        InitializeComponent()
        LoadCategories()
        If _bookId > 0 Then
            LoadBookData()
        Else
            lblFormStatus.Text = "Mode: New Book"
            lblFormStatus.ForeColor = Color.Green
        End If
        
        If Program.IsLocked Then
            DisableEditing()
            lblFormStatus.Text = "Database Locked (Read-Only)"
            lblFormStatus.ForeColor = Color.Red
        End If
        ThemeManager.ApplyTheme(Me)
    End Sub
    
    Private Sub InitializeComponent()
        Me.Text = If(_bookId > 0, "Edit Book Details", "Add New Book")
        Me.Size = New Size(850, 620)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Font = New Font("Segoe UI", Program.BaseFontSize)
        
        ' Tab Control
        tabCtrl = New TabControl() With {.Dock = DockStyle.Fill}
        
        ' Tab 1: Basic Info
        tpBasic = New TabPage("Basic Information")
        Dim glBasic As New TableLayoutPanel() With {
            .ColumnCount = 2, .RowCount = 9, 
            .Dock = DockStyle.Fill, .Padding = New Padding(15),
            .AutoScroll = True
        }
        glBasic.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        glBasic.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 70.0F))
        
        glBasic.Controls.Add(New Label() With {.Text = "Title *:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 0)
        txtTitle = New TextBox() With {.Width = 400}
        glBasic.Controls.Add(txtTitle, 1, 0)
        
        glBasic.Controls.Add(New Label() With {.Text = "Primary Author *:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 1)
        txtAuthor = New TextBox() With {.Width = 400}
        glBasic.Controls.Add(txtAuthor, 1, 1)
        
        glBasic.Controls.Add(New Label() With {.Text = "Category / Genre:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 2)
        cmbCategory = New ComboBox() With {.Width = 250, .DropDownStyle = ComboBoxStyle.DropDownList}
        glBasic.Controls.Add(cmbCategory, 1, 2)
        
        glBasic.Controls.Add(New Label() With {.Text = "Publisher:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 3)
        txtPublisher = New TextBox() With {.Width = 400}
        glBasic.Controls.Add(txtPublisher, 1, 3)
        
        glBasic.Controls.Add(New Label() With {.Text = "Publication Year:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 4)
        txtPubYear = New TextBox() With {.Width = 100}
        glBasic.Controls.Add(txtPubYear, 1, 4)
        
        glBasic.Controls.Add(New Label() With {.Text = "Pages:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 5)
        txtPages = New TextBox() With {.Width = 100}
        glBasic.Controls.Add(txtPages, 1, 5)
        
        glBasic.Controls.Add(New Label() With {.Text = "ISBN:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 6)
        txtISBN = New TextBox() With {.Width = 250}
        glBasic.Controls.Add(txtISBN, 1, 6)
        
        glBasic.Controls.Add(New Label() With {.Text = "Series:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 7)
        txtSeries = New TextBox() With {.Width = 250}
        glBasic.Controls.Add(txtSeries, 1, 7)
        
        glBasic.Controls.Add(New Label() With {.Text = "Copy Number:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 8)
        txtCopyNum = New TextBox() With {.Width = 100}
        glBasic.Controls.Add(txtCopyNum, 1, 8)
        
        tpBasic.Controls.Add(glBasic)
        tabCtrl.TabPages.Add(tpBasic)
        
        ' Tab 2: Co-Authors
        tpAuthors = New TabPage("Co-Authors")
        Dim pnlAuthors As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(15)}
        Dim lblCo As New Label() With {.Text = "Co-Authors Text (comma separated):", .Location = New Point(15, 10), .AutoSize = True}
        pnlAuthors.Controls.Add(lblCo)
        txtCoAuthors = New TextBox() With {.Location = New Point(15, 32), .Width = 780}
        pnlAuthors.Controls.Add(txtCoAuthors)
        
        Dim lblOther As New Label() With {.Text = "Additional Authors (Author 2 to 10):", .Location = New Point(15, 70), .AutoSize = True, .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)}
        pnlAuthors.Controls.Add(lblOther)
        
        Dim glAuthors As New TableLayoutPanel() With {
            .ColumnCount = 4, .RowCount = 5,
            .Location = New Point(15, 100), .Size = New Size(780, 220)
        }
        glAuthors.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 15.0F))
        glAuthors.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35.0F))
        glAuthors.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 15.0F))
        glAuthors.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35.0F))
        
        For idx As Integer = 2 To 10
            Dim labelText = "Author " & idx & ":"
            txtAuthors2To10(idx - 2) = New TextBox() With {.Width = 220}
            Dim cellRow = (idx - 2) \ 2
            Dim cellCol = ((idx - 2) Mod 2) * 2
            glAuthors.Controls.Add(New Label() With {.Text = labelText, .Anchor = AnchorStyles.Left, .AutoSize = True}, cellCol, cellRow)
            glAuthors.Controls.Add(txtAuthors2To10(idx - 2), cellCol + 1, cellRow)
        Next
        pnlAuthors.Controls.Add(glAuthors)
        tpAuthors.Controls.Add(pnlAuthors)
        tabCtrl.TabPages.Add(tpAuthors)
        
        ' Tab 3: Tech details
        tpTech = New TabPage("Technical Details")
        Dim glTech As New TableLayoutPanel() With {
            .ColumnCount = 2, .RowCount = 6,
            .Dock = DockStyle.Fill, .Padding = New Padding(15),
            .AutoScroll = True
        }
        glTech.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        glTech.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 75.0F))
        
        glTech.Controls.Add(New Label() With {.Text = "Language:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 0)
        txtLanguage = New TextBox() With {.Width = 250}
        glTech.Controls.Add(txtLanguage, 1, 0)
        
        glTech.Controls.Add(New Label() With {.Text = "Weight:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 1)
        txtWeight = New TextBox() With {.Width = 250}
        glTech.Controls.Add(txtWeight, 1, 1)
        
        glTech.Controls.Add(New Label() With {.Text = "Dimensions:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 2)
        txtDimensions = New TextBox() With {.Width = 250}
        glTech.Controls.Add(txtDimensions, 1, 2)
        
        glTech.Controls.Add(New Label() With {.Text = "LCCN:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 3)
        txtLCCN = New TextBox() With {.Width = 250}
        glTech.Controls.Add(txtLCCN, 1, 3)
        
        glTech.Controls.Add(New Label() With {.Text = "OCLC:", .Anchor = AnchorStyles.Left, .AutoSize = True}, 0, 4)
        txtOCLC = New TextBox() With {.Width = 250}
        glTech.Controls.Add(txtOCLC, 1, 4)
        
        glTech.Controls.Add(New Label() With {.Text = "Subjects:", .Anchor = AnchorStyles.Top, .AutoSize = True}, 0, 5)
        txtSubjects = New TextBox() With {.Width = 500, .Multiline = True, .Height = 100, .ScrollBars = ScrollBars.Vertical}
        glTech.Controls.Add(txtSubjects, 1, 5)
        
        tpTech.Controls.Add(glTech)
        tabCtrl.TabPages.Add(tpTech)
        
        ' Tab 4: Description & Covers
        tpDescCovers = New TabPage("Description & Covers")
        Dim pnlDescCovers As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(15)}
        
        Dim lblD As New Label() With {.Text = "Description / Summary:", .Location = New Point(15, 10), .AutoSize = True}
        pnlDescCovers.Controls.Add(lblD)
        txtDescription = New TextBox() With {.Location = New Point(15, 32), .Width = 400, .Height = 180, .Multiline = True, .ScrollBars = ScrollBars.Vertical}
        pnlDescCovers.Controls.Add(txtDescription)
        
        Dim lblCov As New Label() With {.Text = "Front Cover URL / Path:", .Location = New Point(15, 230), .AutoSize = True}
        pnlDescCovers.Controls.Add(lblCov)
        txtCoverPath = New TextBox() With {.Location = New Point(15, 252), .Width = 290}
        pnlDescCovers.Controls.Add(txtCoverPath)
        btnBrowseCover = New Button() With {.Text = "Browse...", .Location = New Point(315, 250), .Width = 100, .Height = 26}
        AddHandler btnBrowseCover.Click, Sub() BrowseImage(txtCoverPath, pbCover)
        pnlDescCovers.Controls.Add(btnBrowseCover)
        
        Dim lblBack As New Label() With {.Text = "Back Cover URL / Path:", .Location = New Point(15, 290), .AutoSize = True}
        pnlDescCovers.Controls.Add(lblBack)
        txtBackPath = New TextBox() With {.Location = New Point(15, 312), .Width = 290}
        pnlDescCovers.Controls.Add(txtBackPath)
        btnBrowseBack = New Button() With {.Text = "Browse...", .Location = New Point(315, 310), .Width = 100, .Height = 26}
        AddHandler btnBrowseBack.Click, Sub() BrowseImage(txtBackPath, pbBack)
        pnlDescCovers.Controls.Add(btnBrowseBack)
        
        ' PictureBoxes for preview
        pbCover = New PictureBox() With {.Location = New Point(440, 20), .Width = 160, .Height = 220, .BorderStyle = BorderStyle.FixedSingle, .SizeMode = PictureBoxSizeMode.Zoom}
        pnlDescCovers.Controls.Add(pbCover)
        Dim lblCoverPrev As New Label() With {.Text = "Front Cover Preview", .Location = New Point(440, 245), .Width = 160, .TextAlign = ContentAlignment.MiddleCenter}
        pnlDescCovers.Controls.Add(lblCoverPrev)
        
        pbBack = New PictureBox() With {.Location = New Point(620, 20), .Width = 160, .Height = 220, .BorderStyle = BorderStyle.FixedSingle, .SizeMode = PictureBoxSizeMode.Zoom}
        pnlDescCovers.Controls.Add(pbBack)
        Dim lblBackPrev As New Label() With {.Text = "Back Cover Preview", .Location = New Point(620, 245), .Width = 160, .TextAlign = ContentAlignment.MiddleCenter}
        pnlDescCovers.Controls.Add(lblBackPrev)
        
        tpDescCovers.Controls.Add(pnlDescCovers)
        tabCtrl.TabPages.Add(tpDescCovers)
        
        ' Tab 5: Scanner / Lookup
        tpLookup = New TabPage("ISBN Scanner / Lookup")
        Dim pnlLookup As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(20)}
        Dim lblScan As New Label() With {.Text = "Enter or Scan ISBN (10 or 13 digits):", .Location = New Point(20, 20), .AutoSize = True, .Font = New Font("Segoe UI", 11, FontStyle.Bold)}
        pnlLookup.Controls.Add(lblScan)
        
        txtScannerInput = New TextBox() With {.Location = New Point(20, 50), .Width = 350, .Font = New Font("Segoe UI", 12)}
        pnlLookup.Controls.Add(txtScannerInput)
        
        btnGoogleLookup = New Button() With {
            .Text = "🔍 Google Books Lookup",
            .Location = New Point(20, 95),
            .Width = 210,
            .Height = 35,
            .BackColor = Color.FromArgb(76, 175, 80),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnGoogleLookup.FlatAppearance.BorderSize = 0
        AddHandler btnGoogleLookup.Click, AddressOf GoogleLookupClick
        pnlLookup.Controls.Add(btnGoogleLookup)
        
        btnGeminiLookup = New Button() With {
            .Text = "🤖 AI Lookup (Gemini)",
            .Location = New Point(240, 95),
            .Width = 210,
            .Height = 35,
            .BackColor = Color.FromArgb(156, 39, 176),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnGeminiLookup.FlatAppearance.BorderSize = 0
        AddHandler btnGeminiLookup.Click, AddressOf GeminiLookupClick
        pnlLookup.Controls.Add(btnGeminiLookup)
        
        lblLookupStatus = New Label() With {
            .Text = "Enter ISBN and choose a lookup service above to pre-fill metadata fields.",
            .Location = New Point(20, 160),
            .Width = 700,
            .Height = 80,
            .ForeColor = Color.DimGray
        }
        pnlLookup.Controls.Add(lblLookupStatus)
        
        tpLookup.Controls.Add(pnlLookup)
        tabCtrl.TabPages.Add(tpLookup)
        
        ' Main container panel for tabs
        Dim pnlMain As New Panel() With {
            .Dock = DockStyle.Fill
        }
        pnlMain.Controls.Add(tabCtrl)
        Me.Controls.Add(pnlMain)
        
        ' Footer Panel
        Dim pnlFooter As New Panel() With {
            .Dock = DockStyle.Bottom,
            .Height = 50,
            .BackColor = Color.FromArgb(236, 239, 241)
        }
        Me.Controls.Add(pnlFooter)
        
        lblFormStatus = New Label() With {
            .Text = "",
            .Location = New Point(15, 17),
            .AutoSize = True,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        pnlFooter.Controls.Add(lblFormStatus)
        
        btnSave = New Button() With {
            .Text = "💾 Save Changes",
            .Location = New Point(610, 10),
            .Width = 130,
            .Height = 30,
            .BackColor = Color.FromArgb(46, 125, 50),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnSave.FlatAppearance.BorderSize = 0
        AddHandler btnSave.Click, AddressOf btnSave_Click
        pnlFooter.Controls.Add(btnSave)
        
        btnCancel = New Button() With {
            .Text = "Cancel",
            .Location = New Point(750, 10),
            .Width = 80,
            .Height = 30,
            .BackColor = Color.FromArgb(120, 144, 156),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnCancel.FlatAppearance.BorderSize = 0
        AddHandler btnCancel.Click, Sub() Me.Close()
        pnlFooter.Controls.Add(btnCancel)
    End Sub
    
    Private Sub LoadCategories()
        cmbCategory.Items.Clear()
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT CatID, CategoryName FROM BookCategories ORDER BY CategoryName", conn)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        cmbCategory.Items.Add(New CategoryItem(Convert.ToInt32(reader("CatID")), reader("CategoryName").ToString()))
                    End While
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading categories: " & ex.Message)
        End Try
    End Sub
    
    Private Sub LoadBookData()
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT * FROM Books WHERE ID = ?", conn)
                cmd.Parameters.AddWithValue("?", _bookId)
                Using reader = cmd.ExecuteReader()
                    If reader.Read() Then
                        txtTitle.Text = reader("Title").ToString()
                        txtAuthor.Text = reader("Author").ToString()
                        txtPublisher.Text = reader("Publisher").ToString()
                        txtPubYear.Text = reader("PubYear").ToString()
                        txtPages.Text = reader("Pages").ToString()
                        txtISBN.Text = reader("ISBN").ToString()
                        txtSeries.Text = reader("Series").ToString()
                        txtCopyNum.Text = reader("CopyNum").ToString()
                        txtCoAuthors.Text = reader("CoAuthors").ToString()
                        
                        For i As Integer = 2 To 10
                            Dim fieldName = "Author" & i
                            If reader(fieldName) IsNot DBNull.Value Then
                                txtAuthors2To10(i - 2).Text = reader(fieldName).ToString()
                            End If
                        Next
                        
                        txtLanguage.Text = reader("Language").ToString()
                        txtWeight.Text = reader("Weight").ToString()
                        txtDimensions.Text = reader("Dimensions").ToString()
                        txtLCCN.Text = reader("LCCN").ToString()
                        txtOCLC.Text = reader("OCLC").ToString()
                        txtSubjects.Text = reader("Subjects").ToString()
                        txtDescription.Text = reader("Description").ToString()
                        txtCoverPath.Text = reader("CoverPath").ToString()
                        txtBackPath.Text = reader("BackPath").ToString()
                        
                        Dim catIdVal = reader("CategoryID")
                        If catIdVal IsNot Nothing AndAlso catIdVal IsNot DBNull.Value Then
                            Dim idToFind = Convert.ToInt32(catIdVal)
                            For Each item As CategoryItem In cmbCategory.Items
                                If item.CatID = idToFind Then
                                    cmbCategory.SelectedItem = item
                                    Exit For
                                End If
                            Next
                        End If
                        
                        ' If category is not selected by ID, try selecting by Genre text matching
                        If cmbCategory.SelectedIndex = -1 Then
                            Dim genreVal = reader("Genre").ToString()
                            If Not String.IsNullOrEmpty(genreVal) Then
                                For Each item As CategoryItem In cmbCategory.Items
                                    If item.CategoryName.Equals(genreVal, StringComparison.OrdinalIgnoreCase) Then
                                        cmbCategory.SelectedItem = item
                                        Exit For
                                    End If
                                Next
                            End If
                        End If
                        
                        lblFormStatus.Text = "Mode: Editing Book ID " & _bookId
                        lblFormStatus.ForeColor = Color.FromArgb(30, 136, 229)
                        
                        ' Load images
                        Program.LoadImageSafe(pbCover, txtCoverPath.Text)
                        Program.LoadImageSafe(pbBack, txtBackPath.Text)
                    End If
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading book details: " & ex.Message)
        End Try
    End Sub
    
    Private Sub DisableEditing()
        txtTitle.ReadOnly = True
        txtAuthor.ReadOnly = True
        cmbCategory.Enabled = False
        txtPublisher.ReadOnly = True
        txtPubYear.ReadOnly = True
        txtPages.ReadOnly = True
        txtISBN.ReadOnly = True
        txtSeries.ReadOnly = True
        txtCopyNum.ReadOnly = True
        txtCoAuthors.ReadOnly = True
        For i As Integer = 0 To 8
            txtAuthors2To10(i).ReadOnly = True
        Next
        txtLanguage.ReadOnly = True
        txtWeight.ReadOnly = True
        txtDimensions.ReadOnly = True
        txtLCCN.ReadOnly = True
        txtOCLC.ReadOnly = True
        txtSubjects.ReadOnly = True
        txtDescription.ReadOnly = True
        txtCoverPath.ReadOnly = True
        txtBackPath.ReadOnly = True
        btnBrowseCover.Enabled = False
        btnBrowseBack.Enabled = False
        btnGoogleLookup.Enabled = False
        btnGeminiLookup.Enabled = False
        txtScannerInput.ReadOnly = True
        btnSave.Enabled = False
    End Sub
    
    Private Sub BrowseImage(txtPath As TextBox, pb As PictureBox)
        Using ofd As New OpenFileDialog()
            ofd.Filter = "Image Files (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|All files (*.*)|*.*"
            If ofd.ShowDialog() = DialogResult.OK Then
                txtPath.Text = ofd.FileName
                Program.LoadImageSafe(pb, ofd.FileName)
            End If
        End Using
    End Sub
    
    Private Sub GoogleLookupClick(sender As Object, e As EventArgs)
        Dim isbn = txtScannerInput.Text.Trim()
        If String.IsNullOrEmpty(isbn) Then
            MessageBox.Show("Please enter an ISBN to lookup first.", "Lookup Hint")
            Return
        End If
        Me.Cursor = Cursors.WaitCursor
        lblLookupStatus.Text = "Performing Google Books API search..."
        lblLookupStatus.ForeColor = Color.Blue
        Try
            Dim title As String = "", author As String = "", publisher As String = "", year As String = ""
            Dim found As Boolean = False
            Dim gUrl As String = "https://www.googleapis.com/books/v1/volumes?q=isbn:" & isbn
            Dim gReq As HttpWebRequest = DirectCast(WebRequest.Create(gUrl), HttpWebRequest)
            gReq.Timeout = 10000
            
            Using gResp As HttpWebResponse = DirectCast(gReq.GetResponse(), HttpWebResponse)
                Using sr As New StreamReader(gResp.GetResponseStream())
                    Dim json As String = sr.ReadToEnd()
                    If json.Contains("""totalItems"": 1") Or json.Contains("""totalItems"": 2") Or json.Contains("""totalItems"":") AndAlso Not json.Contains("""totalItems"": 0") Then
                        title = Regex.Match(json, """title"":\s*""(.*?)""").Groups(1).Value
                        author = Regex.Match(json, """authors"":\s*\[\s*""(.*?)""").Groups(1).Value
                        publisher = Regex.Match(json, """publisher"":\s*""(.*?)""").Groups(1).Value
                        year = Regex.Match(json, """publishedDate"":\s*""(\d{4})").Groups(1).Value
                        found = True
                    End If
                End Using
            End Using
            
            ' Fallback to ISBNSearch.org scraping
            If Not found Then
                lblLookupStatus.Text = "Google Books failed. Trying fallback scraping..."
                Dim url As String = "https://isbnsearch.org/isbn/" & isbn
                Dim html As String = ""
                Dim req As HttpWebRequest = DirectCast(WebRequest.Create(url), HttpWebRequest)
                req.UserAgent = "Mozilla/5.0"
                req.Timeout = 10000
                Using resp As HttpWebResponse = DirectCast(req.GetResponse(), HttpWebResponse)
                    Using sr As New StreamReader(resp.GetResponseStream())
                        html = sr.ReadToEnd()
                    End Using
                End Using
                title = Regex.Match(html, "<h2>(.*?)</h2>").Groups(1).Value
                author = Regex.Match(html, "<b>Author:</b> (.*?)</p>").Groups(1).Value
                publisher = Regex.Match(html, "<b>Publisher:</b> (.*?)</p>").Groups(1).Value
                Dim publishedStr As String = Regex.Match(html, "<b>Published:</b> (.*?)</p>").Groups(1).Value
                year = Regex.Match(publishedStr, "(\d{4})").Groups(1).Value
                If Not String.IsNullOrEmpty(title) Then found = True
            End If
            
            If found Then
                txtTitle.Text = title
                txtAuthor.Text = author
                txtPublisher.Text = publisher
                txtPubYear.Text = year
                txtISBN.Text = isbn
                lblLookupStatus.Text = "Successfully filled basic metadata from ISBN!"
                lblLookupStatus.ForeColor = Color.Green
                tabCtrl.SelectedTab = tpBasic
                MessageBox.Show("Book metadata pre-filled successfully!", "Lookup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                lblLookupStatus.Text = "ISBN not found on Google Books or ISBNSearch fallback."
                lblLookupStatus.ForeColor = Color.OrangeRed
                MessageBox.Show("No data found for this ISBN.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            lblLookupStatus.Text = "Error during lookup: " & ex.Message
            lblLookupStatus.ForeColor = Color.Red
            MessageBox.Show("Lookup failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Me.Cursor = Cursors.Default
        End Try
    End Sub
    
    Private Sub GeminiLookupClick(sender As Object, e As EventArgs)
        Dim isbn = txtScannerInput.Text.Trim()
        If String.IsNullOrEmpty(isbn) Then
            MessageBox.Show("Please enter an ISBN to lookup first.", "Lookup Hint")
            Return
        End If
        
        ' Retrieve or prompt for API key
        Dim apiKey As String = ""
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmdGet As New OleDbCommand("SELECT SettingValue FROM Settings WHERE KeyName = 'GeminiApiKey'", conn)
                Dim res = cmdGet.ExecuteScalar()
                If res IsNot Nothing AndAlso res IsNot DBNull.Value Then apiKey = res.ToString()
            End Using
        Catch
        End Try
        
        If String.IsNullOrEmpty(apiKey) Then
            Dim keyPrompt = "Enter your Gemini API Key (from aistudio.google.com). It will be saved for future lookups:"
            apiKey = Interaction.InputBox(keyPrompt, "Gemini API Key Required", "")
            If String.IsNullOrEmpty(apiKey) Then Return
            
            ' Save key
            Try
                Using conn As New OleDbConnection(Program.connString)
                    conn.Open()
                    Dim cmdU As New OleDbCommand("UPDATE Settings SET SettingValue = ? WHERE KeyName = 'GeminiApiKey'", conn)
                    cmdU.Parameters.AddWithValue("?", apiKey)
                    If cmdU.ExecuteNonQuery() = 0 Then
                        Dim cmdI As New OleDbCommand("INSERT INTO Settings (KeyName, SettingValue) VALUES ('GeminiApiKey', ?)", conn)
                        cmdI.Parameters.AddWithValue("?", apiKey)
                        cmdI.ExecuteNonQuery()
                    End If
                End Using
            Catch
            End Try
        End If
        
        Me.Cursor = Cursors.WaitCursor
        lblLookupStatus.Text = "Connecting to Gemini AI..."
        lblLookupStatus.ForeColor = Color.Blue
        Try
            Dim prompt = "You are a strict bibliographic cataloging assistant. The user will provide an ISBN (10 or 13 digits) or market barcode (such as Egyptian barcode starting with 622): '" & isbn & "'. " & _
                         "CRITICAL RULES: " & _
                         "1. DO NOT GUESS, FABRICATE, OR HALLUCINATE ANY BOOK DATA. " & _
                         "2. Do not return famous fallback books just because the barcode prefix matches a country (like 977 or 622 for Egypt). " & _
                         "3. If it starts with 622, it is an Egyptian market/educational publication barcode. If 978/979/977, it is an ISBN. " & _
                         "4. Prefer original script (e.g. Arabic script for Arabic books). " & _
                         "5. If you are not 100% certain of the exact book matching this specific code, you MUST return null for title and author, and set found: false. " & _
                         "Return ONLY a valid JSON object: " & _
                         "{'found': boolean, 'title': string or null, 'author': string or null, 'publisher': string or null, 'publicationYear': string or null, 'language': string or null, 'category': string or null, 'summary': string or null}"
            
            Dim q = Chr(34)
            Dim promptEscaped = prompt.Replace(q, "'")
            Dim candidateModels = New String() {"gemini-3.5-flash-lite", "gemini-3.5-flash", "gemini-3.1-flash-lite", "gemini-3.8-flash"}
            Dim innerJson = ""
            Dim lastError = ""
            
            For Each model In candidateModels
                For Each withSearch In New Boolean() {True, False}
                    Try
                        If withSearch Then
                            lblLookupStatus.Text = "Searching web for ISBN via Gemini (" & model & ")..."
                        Else
                            lblLookupStatus.Text = "Verifying record via Gemini (" & model & ")..."
                        End If
                        Application.DoEvents()
                        
                        Dim endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" & model & ":generateContent?key=" & apiKey
                        Dim jsonBody As String
                        If withSearch Then
                            jsonBody = "{" & q & "contents" & q & ":[{" & q & "parts" & q & ":[{" & q & "text" & q & ":" & q & promptEscaped & q & "}]}]," & _
                                       q & "tools" & q & ":[{" & q & "googleSearch" & q & ":{}}]," & _
                                       q & "generationConfig" & q & ":{" & q & "temperature" & q & ":0.0}}"
                        Else
                            jsonBody = "{" & q & "contents" & q & ":[{" & q & "parts" & q & ":[{" & q & "text" & q & ":" & q & promptEscaped & q & "}]}]," & _
                                       q & "generationConfig" & q & ":{" & q & "temperature" & q & ":0.0}}"
                        End If
                        
                        Dim responseText = ""
                        Dim req As HttpWebRequest = DirectCast(WebRequest.Create(endpoint), HttpWebRequest)
                        req.Method = "POST"
                        req.ContentType = "application/json"
                        req.Timeout = 20000
                        
                        Dim bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody)
                        req.ContentLength = bodyBytes.Length
                        Using stream = req.GetRequestStream()
                            stream.Write(bodyBytes, 0, bodyBytes.Length)
                        End Using
                        
                        Using resp As HttpWebResponse = DirectCast(req.GetResponse(), HttpWebResponse)
                            Using reader As New StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8)
                                responseText = reader.ReadToEnd()
                            End Using
                        End Using
                        
                        Dim textMatch = Regex.Match(responseText, """" & "text" & """" & "\s*:\s*""((?:[^""]|\\"")*)""", RegexOptions.Singleline)
                        If textMatch.Success Then
                            innerJson = textMatch.Groups(1).Value
                            innerJson = innerJson.Replace("\n", " ").Replace(Chr(92) & Chr(34), Chr(34)).Replace(Chr(92) & "/", "/")
                            If Not String.IsNullOrEmpty(innerJson) Then
                                Exit For
                            End If
                        End If
                    Catch exWeb As WebException
                        Dim errBody = ""
                        If exWeb.Response IsNot Nothing Then
                            Using errReader As New StreamReader(exWeb.Response.GetResponseStream(), System.Text.Encoding.UTF8)
                                errBody = errReader.ReadToEnd()
                            End Using
                        End If
                        If withSearch AndAlso (exWeb.Message.Contains("(429)") OrElse errBody.ToLower().Contains("quota")) Then
                            Continue For
                        End If
                        lastError = If(Not String.IsNullOrEmpty(errBody), errBody, exWeb.Message)
                        Exit For
                    Catch exModel As Exception
                        lastError = exModel.Message
                        Exit For
                    End Try
                Next
                If Not String.IsNullOrEmpty(innerJson) Then Exit For
            Next
            
            If String.IsNullOrEmpty(innerJson) Then
                lblLookupStatus.Text = "Gemini failed to resolve. " & lastError
                lblLookupStatus.ForeColor = Color.OrangeRed
                MessageBox.Show("Gemini AI lookup did not yield book metadata: " & lastError, "AI Lookup Failed")
                Return
            End If
            
            Dim isFound = innerJson.ToLower().Contains("""found"": true") OrElse innerJson.ToLower().Contains("""found"":true")
            Dim titleExtracted = Program.GeminiExtractField(innerJson, "title")
            
            If (Not isFound) OrElse String.IsNullOrEmpty(titleExtracted) OrElse titleExtracted = "null" Then
                lblLookupStatus.Text = "لم يتم العثور على بيانات مؤكدة لهذا الرقم"
                lblLookupStatus.ForeColor = Color.DarkOrange
                MessageBox.Show("لم يتم العثور على بيانات مؤكدة لهذا الرقم، يرجى إدخال البيانات يدوياً أو مسح الغلاف", "تنبيه التحقق من الكتاب", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            
            txtTitle.Text = titleExtracted
            txtAuthor.Text = Program.GeminiExtractField(innerJson, "author")
            txtPublisher.Text = Program.GeminiExtractField(innerJson, "publisher")
            Dim pubY = Program.GeminiExtractField(innerJson, "publicationYear")
            If String.IsNullOrEmpty(pubY) OrElse pubY = "null" Then pubY = Program.GeminiExtractField(innerJson, "year")
            txtPubYear.Text = If(pubY <> "null", pubY, "")
            Dim langVal = Program.GeminiExtractField(innerJson, "language")
            txtLanguage.Text = If(langVal <> "null", langVal, "Arabic")
            Dim catVal = Program.GeminiExtractField(innerJson, "category")
            If String.IsNullOrEmpty(catVal) OrElse catVal = "null" Then catVal = Program.GeminiExtractField(innerJson, "subjects")
            txtSubjects.Text = If(catVal <> "null", catVal, "")
            Dim sumVal = Program.GeminiExtractField(innerJson, "summary")
            If String.IsNullOrEmpty(sumVal) OrElse sumVal = "null" Then sumVal = Program.GeminiExtractField(innerJson, "description")
            txtDescription.Text = If(sumVal <> "null", sumVal, "")
            txtISBN.Text = isbn
            
            lblLookupStatus.Text = "Successfully verified book metadata using Gemini AI!"
            lblLookupStatus.ForeColor = Color.Green
            tabCtrl.SelectedTab = tpBasic
            MessageBox.Show("تم التحقق بنجاح وتعبئة بيانات الكتاب بواسطة الذكاء الاصطناعي!", "نجاح التحقق", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            Dim msg = "Gemini API error: " & ex.Message
            If ex.Message.Contains("(429)") Then
                msg = "AI Rate Limit Reached (Error 429). Please wait a minute before trying again."
            End If
            lblLookupStatus.Text = "AI Lookup Failed: " & ex.Message
            lblLookupStatus.ForeColor = Color.Red
            MessageBox.Show(msg, "AI Lookup Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Me.Cursor = Cursors.Default
        End Try
    End Sub
    
    Private Sub btnSave_Click(sender As Object, e As EventArgs)
        Dim titleStr = txtTitle.Text.Trim()
        Dim authorStr = txtAuthor.Text.Trim()
        
        If String.IsNullOrEmpty(titleStr) OrElse String.IsNullOrEmpty(authorStr) Then
            MessageBox.Show("Title and Primary Author are required fields.", "Input Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        
        Dim catIdVal As Object = DBNull.Value
        Dim genreStr As String = ""
        If cmbCategory.SelectedItem IsNot Nothing Then
            Dim catItem = DirectCast(cmbCategory.SelectedItem, CategoryItem)
            catIdVal = catItem.CatID
            genreStr = catItem.CategoryName
        End If
        
        Dim pagesVal As Object = DBNull.Value
        Dim pubYearVal As Object = DBNull.Value
        Dim copyNumVal As Object = DBNull.Value
        
        Dim tempPages As Integer
        If Integer.TryParse(txtPages.Text.Trim(), tempPages) Then pagesVal = tempPages
        Dim tempYear As Integer
        If Integer.TryParse(txtPubYear.Text.Trim(), tempYear) Then pubYearVal = tempYear
        Dim tempCopy As Integer
        If Integer.TryParse(txtCopyNum.Text.Trim(), tempCopy) Then copyNumVal = tempCopy
        
        ' Ensure local images for URLs if applicable
        Dim localCoverPath = txtCoverPath.Text.Trim()
        Dim localBackPath = txtBackPath.Text.Trim()
        Dim isbnStr = txtISBN.Text.Trim()
        
        If localCoverPath.StartsWith("http") Then
            localCoverPath = Program.EnsureLocalImage(_bookId, localCoverPath, "cover")
        End If
        If localBackPath.StartsWith("http") Then
            localBackPath = Program.EnsureLocalImage(_bookId, localBackPath, "back")
        End If
        
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                If _bookId = 0 Then
                    ' INSERT
                    Dim sql = "INSERT INTO Books (Title, Author, Author2, Author3, Author4, Author5, Author6, Author7, Author8, Author9, Author10, CoAuthors, Pages, PubYear, Publisher, ISBN, CopyNum, Series, CoverPath, BackPath, Genre, Description, Language, Weight, Dimensions, LCCN, OCLC, Subjects, AddedBy, CategoryID, LibraryName) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
                    Using cmd As New OleDbCommand(sql, conn)
                        cmd.Parameters.AddWithValue("?", titleStr)
                        cmd.Parameters.AddWithValue("?", authorStr)
                        For i As Integer = 2 To 10
                            cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtAuthors2To10(i - 2).Text), DBNull.Value, txtAuthors2To10(i - 2).Text.Trim()))
                        Next
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtCoAuthors.Text), DBNull.Value, txtCoAuthors.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", pagesVal)
                        cmd.Parameters.AddWithValue("?", pubYearVal)
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtPublisher.Text), DBNull.Value, txtPublisher.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(isbnStr), DBNull.Value, isbnStr))
                        cmd.Parameters.AddWithValue("?", copyNumVal)
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtSeries.Text), DBNull.Value, txtSeries.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(localCoverPath), DBNull.Value, localCoverPath))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(localBackPath), DBNull.Value, localBackPath))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(genreStr), DBNull.Value, genreStr))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtDescription.Text), DBNull.Value, txtDescription.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtLanguage.Text), DBNull.Value, txtLanguage.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtWeight.Text), DBNull.Value, txtWeight.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtDimensions.Text), DBNull.Value, txtDimensions.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtLCCN.Text), DBNull.Value, txtLCCN.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtOCLC.Text), DBNull.Value, txtOCLC.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtSubjects.Text), DBNull.Value, txtSubjects.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", Program.SessionUser)
                        cmd.Parameters.AddWithValue("?", catIdVal)
                        cmd.Parameters.AddWithValue("?", Program.currentLibrary)
                        cmd.ExecuteNonQuery()
                    End Using
                    MessageBox.Show("New book successfully added!", "Added Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Else
                    ' UPDATE
                    Dim sql = "UPDATE Books SET Title=?, Author=?, Author2=?, Author3=?, Author4=?, Author5=?, Author6=?, Author7=?, Author8=?, Author9=?, Author10=?, CoAuthors=?, Pages=?, PubYear=?, Publisher=?, ISBN=?, CopyNum=?, Series=?, CoverPath=?, BackPath=?, Genre=?, Description=?, Language=?, Weight=?, Dimensions=?, LCCN=?, OCLC=?, Subjects=?, EditedBy=?, CategoryID=? WHERE ID=?"
                    Using cmd As New OleDbCommand(sql, conn)
                        cmd.Parameters.AddWithValue("?", titleStr)
                        cmd.Parameters.AddWithValue("?", authorStr)
                        For i As Integer = 2 To 10
                            cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtAuthors2To10(i - 2).Text), DBNull.Value, txtAuthors2To10(i - 2).Text.Trim()))
                        Next
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtCoAuthors.Text), DBNull.Value, txtCoAuthors.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", pagesVal)
                        cmd.Parameters.AddWithValue("?", pubYearVal)
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtPublisher.Text), DBNull.Value, txtPublisher.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(isbnStr), DBNull.Value, isbnStr))
                        cmd.Parameters.AddWithValue("?", copyNumVal)
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtSeries.Text), DBNull.Value, txtSeries.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(localCoverPath), DBNull.Value, localCoverPath))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(localBackPath), DBNull.Value, localBackPath))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(genreStr), DBNull.Value, genreStr))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtDescription.Text), DBNull.Value, txtDescription.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtLanguage.Text), DBNull.Value, txtLanguage.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtWeight.Text), DBNull.Value, txtWeight.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtDimensions.Text), DBNull.Value, txtDimensions.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtLCCN.Text), DBNull.Value, txtLCCN.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtOCLC.Text), DBNull.Value, txtOCLC.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", If(String.IsNullOrEmpty(txtSubjects.Text), DBNull.Value, txtSubjects.Text.Trim()))
                        cmd.Parameters.AddWithValue("?", Program.SessionUser)
                        cmd.Parameters.AddWithValue("?", catIdVal)
                        cmd.Parameters.AddWithValue("?", _bookId)
                        cmd.ExecuteNonQuery()
                    End Using
                    MessageBox.Show("Book details successfully updated!", "Saved Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
                
                Me.DialogResult = DialogResult.OK
                Me.Close()
            End Using
        Catch ex As Exception
            MessageBox.Show("Failed to save book data: " & ex.Message, "Error Saving", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class

' ======================================================================
' CLASS: LibraryManagerForm
' Description: Dialog to manage database libraries (CRUD + Merge).
' ======================================================================
Public Class LibraryManagerForm
    Inherits Form
    Private lstLibraries As ListBox
    Private btnAddLib As Button
    Private btnRenameLib As Button
    Private btnDeleteLib As Button
    Private btnMergeLib As Button
    Private btnClose As Button
    
    Public Sub New()
        InitializeComponent()
        LoadLibraries()
        If Program.IsLocked Then
            btnAddLib.Enabled = False
            btnRenameLib.Enabled = False
            btnDeleteLib.Enabled = False
            btnMergeLib.Enabled = False
        End If
        ThemeManager.ApplyTheme(Me)
    End Sub
    
    Private Sub InitializeComponent()
        Me.Text = "Manage Libraries"
        Me.Size = New Size(520, 380)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Font = New Font("Segoe UI", Program.BaseFontSize)
        
        Dim lblTitle As New Label() With {
            .Text = "System Libraries",
            .Location = New Point(15, 15),
            .Font = New Font("Segoe UI", 12, FontStyle.Bold),
            .AutoSize = True
        }
        Me.Controls.Add(lblTitle)
        
        lstLibraries = New ListBox() With {
            .Location = New Point(15, 45),
            .Width = 320,
            .Height = 270,
            .BorderStyle = BorderStyle.FixedSingle
        }
        Me.Controls.Add(lstLibraries)
        
        Dim xOffset = 350
        Dim yStart = 45
        Dim btnHeight = 35
        Dim btnWidth = 140
        
        btnAddLib = New Button() With {
            .Text = "➕ Add Library",
            .Location = New Point(xOffset, yStart),
            .Width = btnWidth, .Height = btnHeight,
            .BackColor = Color.FromArgb(46, 125, 50),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnAddLib.FlatAppearance.BorderSize = 0
        AddHandler btnAddLib.Click, AddressOf AddLibraryClick
        Me.Controls.Add(btnAddLib)
        
        btnRenameLib = New Button() With {
            .Text = "✏ Rename",
            .Location = New Point(xOffset, yStart + 45),
            .Width = btnWidth, .Height = btnHeight,
            .BackColor = Color.FromArgb(30, 136, 229),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnRenameLib.FlatAppearance.BorderSize = 0
        AddHandler btnRenameLib.Click, AddressOf RenameLibraryClick
        Me.Controls.Add(btnRenameLib)
        
        btnDeleteLib = New Button() With {
            .Text = "✖ Delete",
            .Location = New Point(xOffset, yStart + 90),
            .Width = btnWidth, .Height = btnHeight,
            .BackColor = Color.FromArgb(198, 40, 40),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnDeleteLib.FlatAppearance.BorderSize = 0
        AddHandler btnDeleteLib.Click, AddressOf DeleteLibraryClick
        Me.Controls.Add(btnDeleteLib)
        
        btnMergeLib = New Button() With {
            .Text = "🔗 Merge Libraries",
            .Location = New Point(xOffset, yStart + 135),
            .Width = btnWidth, .Height = btnHeight,
            .BackColor = Color.FromArgb(141, 110, 99),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnMergeLib.FlatAppearance.BorderSize = 0
        AddHandler btnMergeLib.Click, AddressOf MergeLibraryClick
        Me.Controls.Add(btnMergeLib)
        
        btnClose = New Button() With {
            .Text = "Close",
            .Location = New Point(xOffset, yStart + 235),
            .Width = btnWidth, .Height = btnHeight,
            .BackColor = Color.FromArgb(120, 144, 156),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnClose.FlatAppearance.BorderSize = 0
        AddHandler btnClose.Click, Sub() Me.Close()
        Me.Controls.Add(btnClose)
    End Sub
    
    Private Sub LoadLibraries()
        lstLibraries.Items.Clear()
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT LibName FROM Libraries ORDER BY LibName", conn)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        lstLibraries.Items.Add(reader("LibName").ToString())
                    End While
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading libraries: " & ex.Message)
        End Try
    End Sub
    
    Private Sub AddLibraryClick(sender As Object, e As EventArgs)
        Dim libName = Interaction.InputBox("Enter name for the new library:", "Create Library", "").Trim()
        If String.IsNullOrEmpty(libName) Then Return
        
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("INSERT INTO Libraries (LibName) VALUES (?)", conn)
                cmd.Parameters.AddWithValue("?", libName)
                cmd.ExecuteNonQuery()
            End Using
            LoadLibraries()
            MessageBox.Show("Library created successfully!", "Success")
        Catch ex As Exception
            MessageBox.Show("Error creating library: " & ex.Message, "Error")
        End Try
    End Sub
    
    Private Sub RenameLibraryClick(sender As Object, e As EventArgs)
        If lstLibraries.SelectedItem Is Nothing Then
            MessageBox.Show("Please select a library to rename.", "Selection Required")
            Return
        End If
        
        Dim oldName = lstLibraries.SelectedItem.ToString()
        Dim newName = Interaction.InputBox("Enter new name for the library '" & oldName & "':", "Rename Library", oldName).Trim()
        If String.IsNullOrEmpty(newName) OrElse newName = oldName Then Return
        
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                ' Insert new name in Libraries first
                Try
                    Dim cmdI As New OleDbCommand("INSERT INTO Libraries (LibName) VALUES (?)", conn)
                    cmdI.Parameters.AddWithValue("?", newName)
                    cmdI.ExecuteNonQuery()
                Catch ex As Exception
                    MessageBox.Show("A library with that name already exists.", "Naming Collision")
                    Return
                End Try
                
                ' Update books
                Dim cmdU As New OleDbCommand("UPDATE Books SET LibraryName = ? WHERE LibraryName = ?", conn)
                cmdU.Parameters.AddWithValue("?", newName)
                cmdU.Parameters.AddWithValue("?", oldName)
                cmdU.ExecuteNonQuery()
                
                ' Delete old library name
                Dim cmdD As New OleDbCommand("DELETE FROM Libraries WHERE LibName = ?", conn)
                cmdD.Parameters.AddWithValue("?", oldName)
                cmdD.ExecuteNonQuery()
                
                If Program.currentLibrary = oldName Then
                    Program.currentLibrary = newName
                End If
            End Using
            LoadLibraries()
            MessageBox.Show("Library renamed successfully!", "Success")
        Catch ex As Exception
            MessageBox.Show("Error renaming library: " & ex.Message, "Error")
        End Try
    End Sub
    
    Private Sub DeleteLibraryClick(sender As Object, e As EventArgs)
        If lstLibraries.SelectedItem Is Nothing Then
            MessageBox.Show("Please select a library to delete.", "Selection Required")
            Return
        End If
        
        Dim selectedLib = lstLibraries.SelectedItem.ToString()
        
        ' Prevent deleting last library
        If lstLibraries.Items.Count <= 1 Then
            MessageBox.Show("Cannot delete the only remaining library in the database.", "Operation Cancelled")
            Return
        End If
        
        Dim msg = String.Format("Are you sure you want to delete the library '{0}' and ALL of the books in it? This is irreversible!", selectedLib)
        Dim res = MessageBox.Show(msg, "Verify Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If res <> DialogResult.Yes Then Return
        
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                ' Delete books
                Dim cmdB As New OleDbCommand("DELETE FROM Books WHERE LibraryName = ?", conn)
                cmdB.Parameters.AddWithValue("?", selectedLib)
                cmdB.ExecuteNonQuery()
                
                ' Delete library
                Dim cmdL As New OleDbCommand("DELETE FROM Libraries WHERE LibName = ?", conn)
                cmdL.Parameters.AddWithValue("?", selectedLib)
                cmdL.ExecuteNonQuery()
                
                If Program.currentLibrary = selectedLib Then
                    For Each item In lstLibraries.Items
                        If item.ToString() <> selectedLib Then
                            Program.currentLibrary = item.ToString()
                            Exit For
                        End If
                    Next
                End If
            End Using
            LoadLibraries()
            MessageBox.Show("Library and its collection deleted.", "Complete")
        Catch ex As Exception
            MessageBox.Show("Error deleting library: " & ex.Message)
        End Try
    End Sub
    
    Private Sub MergeLibraryClick(sender As Object, e As EventArgs)
        If lstLibraries.SelectedItem Is Nothing Then
            MessageBox.Show("Please select the source library to merge.", "Selection Required")
            Return
        End If
        
        Dim sourceLib = lstLibraries.SelectedItem.ToString()
        Dim targetLib = Interaction.InputBox("Enter the exact name of the destination library to merge books into:", "Merge Libraries", "").Trim()
        If String.IsNullOrEmpty(targetLib) OrElse targetLib = sourceLib Then Return
        
        ' Verify target exists
        Dim targetExists = False
        For Each item In lstLibraries.Items
            If item.ToString().Equals(targetLib, StringComparison.OrdinalIgnoreCase) Then
                targetExists = True
                Exit For
            End If
        Next
        
        If Not targetExists Then
            MessageBox.Show("Destination library does not exist.", "Invalid Destination")
            Return
        End If
        
        Dim msg = String.Format("Merge books from '{0}' into '{1}'? The library '{0}' will be removed afterwards.", sourceLib, targetLib)
        If MessageBox.Show(msg, "Confirm Merge", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            Try
                Using conn As New OleDbConnection(Program.connString)
                    conn.Open()
                    Dim cmdU As New OleDbCommand("UPDATE Books SET LibraryName = ? WHERE LibraryName = ?", conn)
                    cmdU.Parameters.AddWithValue("?", targetLib)
                    cmdU.Parameters.AddWithValue("?", sourceLib)
                    cmdU.ExecuteNonQuery()
                    
                    Dim cmdD As New OleDbCommand("DELETE FROM Libraries WHERE LibName = ?", conn)
                    cmdD.Parameters.AddWithValue("?", sourceLib)
                    cmdD.ExecuteNonQuery()
                    
                    If Program.currentLibrary = sourceLib Then
                        Program.currentLibrary = targetLib
                    End If
                End Using
                LoadLibraries()
                MessageBox.Show("Libraries merged successfully!", "Success")
            Catch ex As Exception
                MessageBox.Show("Merge failed: " & ex.Message)
            End Try
        End If
    End Sub
End Class

' ======================================================================
' CLASS: SettingsForm
' Description: System configurations (Database, UI scaling, HTTP Server, Users).
' ======================================================================
Public Class SettingsForm
    Inherits Form
    Private tabCtrl As TabControl
    Private tpDb As TabPage
    Private tpStyle As TabPage
    Private tpTools As TabPage
    Private tpUsers As TabPage
    
    ' DB Controls
    Private txtConnString As TextBox
    Private btnTestConn As Button
    Private btnSaveDb As Button
    
    ' Style Controls
    Private cmbTheme As ComboBox
    Private cmbFontSize As ComboBox
    Private btnSaveStyle As Button
    
    ' Tools Controls
    Private chkWebServer As CheckBox
    Private btnHarvester As Button
    Private btnExportCsv As Button
    
    ' Users Controls
    Private lstUsers As ListBox
    Private btnAddUser As Button
    Private btnDelUser As Button
    Private btnChangePass As Button

    Public Sub New()
        InitializeComponent()
        LoadSettings()
        ThemeManager.ApplyTheme(Me)
    End Sub
    
    Private Sub InitializeComponent()
        Me.Text = "System Configuration & Tools"
        Me.Size = New Size(580, 430)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Font = New Font("Segoe UI", Program.BaseFontSize)
        
        tabCtrl = New TabControl() With {.Dock = DockStyle.Fill}
        
        ' Tab 1: Database
        tpDb = New TabPage("Database Settings")
        Dim pnlDb As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(20)}
        Dim lblConn As New Label() With {.Text = "Database Connection String:", .Location = New Point(20, 20), .AutoSize = True}
        pnlDb.Controls.Add(lblConn)
        txtConnString = New TextBox() With {.Location = New Point(20, 42), .Width = 510, .Height = 120, .Multiline = True, .ScrollBars = ScrollBars.Vertical}
        pnlDb.Controls.Add(txtConnString)
        
        btnTestConn = New Button() With {
            .Text = "⚡ Test Connection",
            .Location = New Point(20, 180),
            .Width = 150, .Height = 32,
            .BackColor = Color.FromArgb(120, 144, 156),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnTestConn.FlatAppearance.BorderSize = 0
        AddHandler btnTestConn.Click, AddressOf TestConnectionClick
        pnlDb.Controls.Add(btnTestConn)
        
        btnSaveDb = New Button() With {
            .Text = "💾 Save DB Settings",
            .Location = New Point(180, 180),
            .Width = 160, .Height = 32,
            .BackColor = Color.FromArgb(46, 125, 50),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnSaveDb.FlatAppearance.BorderSize = 0
        AddHandler btnSaveDb.Click, AddressOf SaveDbClick
        pnlDb.Controls.Add(btnSaveDb)
        
        Dim lblDbNote As New Label() With {
            .Text = "Note: Changes to the database settings will require the application to be restarted.",
            .Location = New Point(20, 230), .Width = 510, .Height = 50, .ForeColor = Color.DimGray
        }
        pnlDb.Controls.Add(lblDbNote)
        tpDb.Controls.Add(pnlDb)
        tabCtrl.TabPages.Add(tpDb)
        
        ' Tab 2: Style
        tpStyle = New TabPage("Appearance Settings")
        Dim pnlStyle As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(20)}
        
        Dim lblTheme As New Label() With {.Text = "Visual Theme Style:", .Location = New Point(20, 20), .AutoSize = True}
        pnlStyle.Controls.Add(lblTheme)
        cmbTheme = New ComboBox() With {.Location = New Point(20, 42), .Width = 200, .DropDownStyle = ComboBoxStyle.DropDownList}
        cmbTheme.Items.AddRange(New String() {"Light Ivory", "Dark Slate"})
        pnlStyle.Controls.Add(cmbTheme)
        
        Dim lblSize As New Label() With {.Text = "User Interface Scale (Font Size):", .Location = New Point(20, 90), .AutoSize = True}
        pnlStyle.Controls.Add(lblSize)
        cmbFontSize = New ComboBox() With {.Location = New Point(20, 112), .Width = 100, .DropDownStyle = ComboBoxStyle.DropDownList}
        cmbFontSize.Items.AddRange(New String() {"8", "9", "10", "11", "12", "14", "16"})
        pnlStyle.Controls.Add(cmbFontSize)
        
        btnSaveStyle = New Button() With {
            .Text = "💾 Apply Appearance",
            .Location = New Point(20, 180),
            .Width = 180, .Height = 32,
            .BackColor = Color.FromArgb(46, 125, 50),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnSaveStyle.FlatAppearance.BorderSize = 0
        AddHandler btnSaveStyle.Click, AddressOf SaveStyleClick
        pnlStyle.Controls.Add(btnSaveStyle)
        tpStyle.Controls.Add(pnlStyle)
        tabCtrl.TabPages.Add(tpStyle)
        
        ' Tab 3: Tools & HTTP
        tpTools = New TabPage("System Tools")
        Dim pnlTools As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(20)}
        
        chkWebServer = New CheckBox() With {
            .Text = "Enable HTTP Dashboard (Port 8080)",
            .Location = New Point(20, 20),
            .Width = 450, .Height = 30
        }
        AddHandler chkWebServer.CheckedChanged, AddressOf WebServerCheckChanged
        pnlTools.Controls.Add(chkWebServer)
        
        btnHarvester = New Button() With {
            .Text = "🖼 Image Harvester (Download all web URLs to local)",
            .Location = New Point(20, 70),
            .Width = 400, .Height = 35,
            .BackColor = Color.FromArgb(141, 110, 99),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnHarvester.FlatAppearance.BorderSize = 0
        AddHandler btnHarvester.Click, AddressOf ImageHarvesterClick
        pnlTools.Controls.Add(btnHarvester)
        
        btnExportCsv = New Button() With {
            .Text = "📊 Export Current Library to CSV Report",
            .Location = New Point(20, 120),
            .Width = 400, .Height = 35,
            .BackColor = Color.FromArgb(84, 110, 122),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnExportCsv.FlatAppearance.BorderSize = 0
        AddHandler btnExportCsv.Click, Sub() Program.ExportBooksToCsv(Program.currentLibrary)
        pnlTools.Controls.Add(btnExportCsv)
        
        tpTools.Controls.Add(pnlTools)
        tabCtrl.TabPages.Add(tpTools)
        
        ' Tab 4: User Provisioning
        tpUsers = New TabPage("User Provisioning")
        Dim pnlUsers As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(15)}
        
        lstUsers = New ListBox() With {
            .Location = New Point(15, 15),
            .Width = 320, .Height = 300,
            .BorderStyle = BorderStyle.FixedSingle
        }
        pnlUsers.Controls.Add(lstUsers)
        
        Dim uxOffset = 350
        Dim uyStart = 15
        Dim ubtnHeight = 32
        Dim ubtnWidth = 150
        
        btnAddUser = New Button() With {
            .Text = "👤 Add Account",
            .Location = New Point(uxOffset, uyStart),
            .Width = ubtnWidth, .Height = ubtnHeight,
            .BackColor = Color.FromArgb(46, 125, 50),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnAddUser.FlatAppearance.BorderSize = 0
        AddHandler btnAddUser.Click, AddressOf AddUserClick
        pnlUsers.Controls.Add(btnAddUser)
        
        btnChangePass = New Button() With {
            .Text = "🔑 New Password",
            .Location = New Point(uxOffset, uyStart + 40),
            .Width = ubtnWidth, .Height = ubtnHeight,
            .BackColor = Color.FromArgb(30, 136, 229),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnChangePass.FlatAppearance.BorderSize = 0
        AddHandler btnChangePass.Click, AddressOf ChangePassClick
        pnlUsers.Controls.Add(btnChangePass)
        
        btnDelUser = New Button() With {
            .Text = "✖ Remove Account",
            .Location = New Point(uxOffset, uyStart + 80),
            .Width = ubtnWidth, .Height = ubtnHeight,
            .BackColor = Color.FromArgb(198, 40, 40),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnDelUser.FlatAppearance.BorderSize = 0
        AddHandler btnDelUser.Click, AddressOf RemoveUserClick
        pnlUsers.Controls.Add(btnDelUser)
        
        tpUsers.Controls.Add(pnlUsers)
        
        ' Hide Tab 4 if current role is not SuperAdmin
        If Program.SessionRole = "SuperAdmin" Then
            tabCtrl.TabPages.Add(tpUsers)
            LoadUsers()
        End If
        
        Me.Controls.Add(tabCtrl)
    End Sub
    
    Private Sub LoadSettings()
        txtConnString.Text = Program.connString
        cmbTheme.SelectedItem = ThemeManager.CurrentTheme
        cmbFontSize.SelectedItem = Convert.ToInt32(Program.BaseFontSize).ToString()
        chkWebServer.Checked = (Program.GetSetting("WebServerEnabled", "True") = "True")
    End Sub
    
    Private Sub TestConnectionClick(sender As Object, e As EventArgs)
        Dim testCs = txtConnString.Text.Trim()
        If String.IsNullOrEmpty(testCs) Then Return
        Me.Cursor = Cursors.WaitCursor
        Try
            Using conn As New OleDbConnection(testCs)
                conn.Open()
            End Using
            MessageBox.Show("Connection test succeeded!", "Test Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Connection test failed: " & ex.Message, "Test Failure", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Me.Cursor = Cursors.Default
        End Try
    End Sub
    
    Private Sub SaveDbClick(sender As Object, e As EventArgs)
        Dim newCs = txtConnString.Text.Trim()
        If String.IsNullOrEmpty(newCs) Then Return
        
        Try
            Dim configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt")
            File.WriteAllText(configPath, newCs)
            Program.connString = newCs
            MessageBox.Show("Database connection saved. The application database schema will be checked and verified.", "Saved")
            Program.InitializeDatabase()
        Catch ex As Exception
            MessageBox.Show("Failed to save connection string: " & ex.Message, "Error")
        End Try
    End Sub
    
    Private Sub SaveStyleClick(sender As Object, e As EventArgs)
        Dim selectedTheme = cmbTheme.SelectedItem.ToString()
        Dim selectedSize = cmbFontSize.SelectedItem.ToString()
        
        ThemeManager.SetTheme(selectedTheme)
        Program.BaseFontSize = Convert.ToSingle(selectedSize)
        
        Program.SaveSetting("AppTheme", selectedTheme)
        Program.SaveSetting("BaseFontSize", selectedSize)
        
        ThemeManager.ApplyTheme(Me)
        Me.Font = New Font("Segoe UI", Program.BaseFontSize)
        
        MessageBox.Show("Visual style applied and saved as defaults.", "Theme Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub
    
    Private Sub WebServerCheckChanged(sender As Object, e As EventArgs)
        Dim enabledVal = If(chkWebServer.Checked, "True", "False")
        Program.SaveSetting("WebServerEnabled", enabledVal)
        If chkWebServer.Checked Then
            Task.Run(Sub() Program.StartWebServer())
        Else
            Program.StopWebServer()
        End If
    End Sub
    
    Private Sub ImageHarvesterClick(sender As Object, e As EventArgs)
        If Program.IsLocked Then
            MessageBox.Show("Database editing is locked. Please unlock first.", "Database Locked")
            Return
        End If
        
        Dim res = MessageBox.Show("Download all external cover URLs locally to local disk storage? This might take some time.", "Start Harvester", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If res <> DialogResult.Yes Then Return
        
        Dim progress As New ProgressForm("Harvesting Book Covers")
        progress.Show()
        progress.pb.Value = 0
        
        Me.Cursor = Cursors.WaitCursor
        Try
            Dim booksToHarvest As New List(Of Tuple(Of Integer, String, String, String))()
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT ID, ISBN, CoverPath, BackPath FROM Books WHERE CoverPath LIKE 'http%' OR BackPath LIKE 'http%'", conn)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim bid = Convert.ToInt32(reader("ID"))
                        Dim isbn = If(reader("ISBN") Is DBNull.Value, "", reader("ISBN").ToString())
                        Dim cp = If(reader("CoverPath") Is DBNull.Value, "", reader("CoverPath").ToString())
                        Dim bp = If(reader("BackPath") Is DBNull.Value, "", reader("BackPath").ToString())
                        booksToHarvest.Add(New Tuple(Of Integer, String, String, String)(bid, isbn, cp, bp))
                    End While
                End Using
                
                If booksToHarvest.Count = 0 Then
                    progress.Close()
                    MessageBox.Show("No external web images to harvest found in current database.", "Harvest Skipped")
                    Return
                End If
                
                progress.pb.Maximum = booksToHarvest.Count
                Dim count = 0
                For Each item In booksToHarvest
                    Dim bid = item.Item1
                    Dim isbn = item.Item2
                    Dim cp = item.Item3
                    Dim bp = item.Item4
                    
                    Dim localCp = cp
                    Dim localBp = bp
                    
                    If cp.StartsWith("http") Then
                        localCp = Program.EnsureLocalImage(bid, cp, "cover")
                    End If
                    If bp.StartsWith("http") Then
                        localBp = Program.EnsureLocalImage(bid, bp, "back")
                    End If
                    
                    If localCp <> cp OrElse localBp <> bp Then
                        Dim cmdU As New OleDbCommand("UPDATE Books SET CoverPath = ?, BackPath = ? WHERE ID = ?", conn)
                        cmdU.Parameters.AddWithValue("?", localCp)
                        cmdU.Parameters.AddWithValue("?", localBp)
                        cmdU.Parameters.AddWithValue("?", bid)
                        cmdU.ExecuteNonQuery()
                        count += 1
                    End If
                    
                    progress.pb.Value += 1
                    progress.lblStatus.Text = String.Format("Downloading images for book {0} of {1}...", progress.pb.Value, booksToHarvest.Count)
                    Application.DoEvents()
                Next
                
                progress.Close()
                MessageBox.Show(String.Format("Finished image harvest! {0} cover images downloaded locally.", count), "Harvest Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Using
        Catch ex As Exception
            progress.Close()
            MessageBox.Show("Harvester error: " & ex.Message, "Error")
        Finally
            Me.Cursor = Cursors.Default
        End Try
    End Sub
    
    ' User Account Provisioning CRUD
    Private Sub LoadUsers()
        lstUsers.Items.Clear()
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT Username, Role FROM Users ORDER BY Username", conn)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        lstUsers.Items.Add(String.Format("{0} [{1}]", reader("Username").ToString(), reader("Role").ToString()))
                    End While
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading user list: " & ex.Message)
        End Try
    End Sub
    
    Private Sub AddUserClick(sender As Object, e As EventArgs)
        Dim u = Interaction.InputBox("Enter username for the new account:", "Add Account", "").Trim()
        If String.IsNullOrEmpty(u) Then Return
        
        Dim p = Interaction.InputBox("Enter password for the new account:", "Set Password", "").Trim()
        If String.IsNullOrEmpty(p) Then Return
        
        Dim r = Interaction.InputBox("Enter role (SuperAdmin, Admin, User):", "Assign Role", "User").Trim()
        If String.IsNullOrEmpty(r) Then Return
        
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("INSERT INTO Users (Username, [Password], Role) VALUES (?, ?, ?)", conn)
                cmd.Parameters.AddWithValue("?", u)
                cmd.Parameters.AddWithValue("?", p)
                cmd.Parameters.AddWithValue("?", r)
                cmd.ExecuteNonQuery()
            End Using
            LoadUsers()
            MessageBox.Show("User account registered successfully!", "Registered")
        Catch ex As Exception
            MessageBox.Show("Failed to create user: " & ex.Message)
        End Try
    End Sub
    
    Private Sub ChangePassClick(sender As Object, e As EventArgs)
        If lstUsers.SelectedItem Is Nothing Then Return
        Dim selected = lstUsers.SelectedItem.ToString()
        Dim uName = selected.Split(" "c)(0)
        
        Dim newPass = Interaction.InputBox("Enter new password for account '" & uName & "':", "Change Password", "").Trim()
        If String.IsNullOrEmpty(newPass) Then Return
        
        Try
            Using conn As New OleDbConnection(Program.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("UPDATE Users SET [Password] = ? WHERE Username = ?", conn)
                cmd.Parameters.AddWithValue("?", newPass)
                cmd.Parameters.AddWithValue("?", uName)
                cmd.ExecuteNonQuery()
            End Using
            MessageBox.Show("Password updated successfully!", "Success")
        Catch ex As Exception
            MessageBox.Show("Failed to update password: " & ex.Message)
        End Try
    End Sub
    
    Private Sub RemoveUserClick(sender As Object, e As EventArgs)
        If lstUsers.SelectedItem Is Nothing Then Return
        Dim selected = lstUsers.SelectedItem.ToString()
        Dim uName = selected.Split(" "c)(0)
        
        If uName.Equals(Program.SessionUser, StringComparison.OrdinalIgnoreCase) Then
            MessageBox.Show("You cannot delete the current logged in administrative account.", "Operation Blocked")
            Return
        End If
        
        If MessageBox.Show("Are you sure you want to remove user account '" & uName & "'?", "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            Try
                Using conn As New OleDbConnection(Program.connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("DELETE FROM Users WHERE Username = ?", conn)
                    cmd.Parameters.AddWithValue("?", uName)
                    cmd.ExecuteNonQuery()
                End Using
                LoadUsers()
                MessageBox.Show("User account removed.", "Success")
            Catch ex As Exception
                MessageBox.Show("Failed to remove user: " & ex.Message)
            End Try
        End If
    End Sub
End Class

