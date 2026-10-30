Imports System
Imports System.Data.OleDb
Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports System.IO
Imports System.Diagnostics
Imports System.Collections.Generic
Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Data
Imports System.Threading.Tasks

' ======================================================================
' CLASS: MainForm
' Description: The central Form container that houses the Welcome Screen 
' and Library Management interfaces. It handles all database transactions.
' ======================================================================
Public Class MainForm
    Inherits Form

    ' --- Global Navigation Controls ---
    Private lblNav As Label
    Private cmbNavigation As ComboBox ' Dropdown switching between Welcome/Library View

    ' --- Core View Panels ---
    Private pnlWelcome As Panel ' Holds the Welcome screen layout
    Private pnlLibrary As Panel ' Holds the Library data grid layout

    ' --- Welcome Screen Controls ---
    Private lblProjectName As Label
    Private pnlDashboard As Panel          ' Container for stats and ticker
    Private lblTotalLibs As Label          ' Database Statistic: Libraries Count
    Private lblTotalBooks As Label         ' Database Statistic: Books Count
    Private pnlTickerContainer As Panel    ' Masks the scrolling ticker
    Private pnlTickerScroll As Panel      ' Moves horizontally to scroll items
    Private timerTicker As Timer           ' Drives the ticker animation
    
    Private lblChooseLib As Label
    Private cmbWelcomeLibraries As ComboBox ' Selection list for switching active libraries
    Private btnUseLib As Button            ' Opens library
    Private btnAddLib As Button            ' Creates a new library
    Private btnRenameLib As Button         ' Renames an existing library
    Private btnDeleteLib As Button         ' Removes a library and all contained books
    Private btnMergeLibs As Button         ' Merges two libraries together

    ' --- Library Viewer Controls ---
    Private dgvBooks As DataGridView       ' Data grid showing the database records
    Private picBookCover As PictureBox     ' Thumbnail for selected book cover
    Private picBookBack As PictureBox      ' Thumbnail for selected book back cover
    Private lblCoverTitle As Label
    Private lblBackTitle As Label
    
    ' --- Inline Book Editor Controls ---
    Private pnlEditor As Panel
    Private pnlEdInner As Panel   ' Inner scrollable content panel
    Private txtEdTitle, txtEdAuthor, txtEdYear, txtEdPages, txtEdCategory, txtEdISBN As TextBox
    Private txtEdSummary, txtEdCoverUrl, txtEdBackUrl As TextBox
    Private btnEdSave, btnEdNew, btnEdDelete, btnEdSearchOnline As Button
    
    Private btnToggleLock As Button        ' Secures/Unsecures the editing panel
    Private btnIncreaseFont As Button      ' UI Font scaling up
    Private btnDecreaseFont As Button      ' UI Font scaling down
    
    Private lblLibraryViewTitle As Label   ' Header displaying the name of the currently opened library

    ' --- Action Buttons ---
    Private btnSearchOnline As Button      
    Private txtScannerInput As TextBox     ' For manual or scanner ISBN entry
    Private btnScanSearch As Button        ' Triggers external ISBN lookup
    Private lblSelectedId As Label         ' Shows ID of currently highlighted Book

    ' --- Application State & Configuration ---
    Private isLocked As Boolean = True
    Private AppPassword As String = "admin123"
    Private dbPath As String = "Library.mdb"
    Private dbPassword As String = "dbpass"
    Public Shared connString As String
    Private currentLibrary As String = "Default Library"
    Private baseFontSize As Single = 10.0F

    ' --- Resolution Mode ---
    Private resolutionMode As String = "Medium"
    Private pnlSettings As Panel
    Private lblSettingsTitle As Label

    ' --- Theme System ---
    Private currentTheme As String = "Clean Charcoal"
    Private pnlHeader As Panel   ' Promoted to field for theming
    Private pnlBody As Panel     ' Promoted to field for theming
    Private pnlScanRow As FlowLayoutPanel   ' Promoted for theming
    Private pnlLockRow As FlowLayoutPanel   ' Promoted for theming
    Private pnlLibTop As Panel              ' Promoted for theming

    ' ======================================================================
    ' Sub: New (Constructor)
    ' Initializes forms and checks if database structures exist.
    ' ======================================================================
    Private configPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt")

    Public Sub New()
        LoadConnectionSettings()
        InitializeDatabase()
        InitializeComponent()
        SetBrowserEmulation()
        LoadTheme()    ' Load saved theme, then paint the UI
        ApplyTheme()
        UpdateUIState()
        LoadLibraries()
        LoadGridLayout()
        ' Wire gradient paint handler
        AddHandler pnlWelcome.Paint, AddressOf pnlWelcome_Paint
        InitRandomGradient()  ' Pick a new palette on every launch
        SwitchView("Welcome Screen")
        Task.Run(Sub() StartWebServer())
    End Sub

    ' Save layout on close
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        SaveGridLayout()
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub LoadConnectionSettings()
        If File.Exists(configPath) Then
            connString = File.ReadAllText(configPath).Trim()
        Else
            ' Default Access setup
            connString = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=Library.mdb;Jet OLEDB:Database Password=dbpass;"
            File.WriteAllText(configPath, connString)
        End If
    End Sub

    ' ======================================================================
    ' Sub: InitializeDatabase
    ' Automatically attempts to setup new "Libraries" tables and "LibraryName"
    ' columns incase the user runs the app from a clean slate.
    ' ======================================================================
    Private Sub InitializeDatabase()
        If String.IsNullOrEmpty(connString) Then Return
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                ' Ensure Libraries table structure exists
                Try
                    Dim cmd As New OleDbCommand("CREATE TABLE Libraries (LibName VARCHAR(255) PRIMARY KEY)", conn)
                    cmd.ExecuteNonQuery()
                Catch
                    ' Catch triggers safely if already created previously
                End Try

                ' Ensure Books table has LibraryName sorting
                Try
                    Dim cmd As New OleDbCommand("ALTER TABLE Books ADD COLUMN LibraryName VARCHAR(255)", conn)
                    cmd.ExecuteNonQuery()
                Catch
                    ' Catch triggers safely if column already exists
                End Try

                ' ADD NEW COLUMNS
                Dim newCols As String() = {"[Author2] VARCHAR(255)", "[Author3] VARCHAR(255)", "[Author4] VARCHAR(255)", "[Author5] VARCHAR(255)", "[Author6] VARCHAR(255)", "[Author7] VARCHAR(255)", "[Author8] VARCHAR(255)", "[Author9] VARCHAR(255)", "[Author10] VARCHAR(255)", "[Pages] INTEGER", "[PubYear] INTEGER", "[CoAuthors] VARCHAR(255)", "[Publisher] VARCHAR(255)", "[ISBN] VARCHAR(50)", "[CopyNum] INTEGER", "[Series] VARCHAR(255)", "[CoverPath] VARCHAR(255)", "[BackPath] VARCHAR(255)", "[Genre] VARCHAR(255)", "[Description] MEMO", "[Language] VARCHAR(50)", "[Weight] VARCHAR(50)", "[Dimensions] VARCHAR(100)", "[LCCN] VARCHAR(50)", "[OCLC] VARCHAR(50)", "[Subjects] MEMO"}
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

                    ' Migration: Genre -> BookCategories (Normalized V3.2.3 Full Seeding)
                    Try
                        ' 1. Create BookCategories if missing
                        Dim cmdCat As New OleDbCommand("CREATE TABLE BookCategories (CatID AUTOINCREMENT PRIMARY KEY, CategoryName VARCHAR(255) UNIQUE)", conn)
                        cmdCat.ExecuteNonQuery()
                    Catch
                    End Try

                    ' 2. Seed 300+ Genres - Optimized (V3.5.1)
                    Dim checkCountCmd As New OleDbCommand("SELECT COUNT(*) FROM BookCategories", conn)
                    If CInt(checkCountCmd.ExecuteScalar()) < 10 Then
                        Dim genresList As String() = {
                            "Fiction", "Non-Fiction", "Science", "History", "Biography", "Fantasy", "Mystery", "Philosophy", "Psychology", "Technology",
                            "Art", "Education", "Law", "Medicine", "Social Science", "Political Science", "Religion", "Language", "Literature", "Computers",
                            "Business", "Economics", "Reference", "Sports", "Music", "Cooking", "Travel", "Poetry", "Drama", "Essays",
                            "Geography", "Archeology", "Sociology", "Mathematics", "Physics", "Chemistry", "Biology", "Astronomy", "Geology", "Engineering",
                            "Architecture", "Photography", "Design", "Gardening", "Nature", "Health", "Parenting", "Self-Help", "Humor", "Juvenile",
                            "Action", "Adventure", "Thriller", "Horror", "Romance", "Western", "War", "Historical Fiction", "Alternative History", "Cyberpunk",
                            "Steampunk", "Dystopian", "Utopian", "Crime", "True Crime", "Espionage", "Short Story", "Anthology", "Graphic Novel", "Manga",
                            "Comic Book", "Children's", "Picture Book", "Young Adult", "Middle Grade", "Mythology", "Folklore", "Fairy Tale", "Paranormal", "Urban Fantasy",
                            "Space Opera", "Hard Science Fiction", "Soft Science Fiction", "Post-Apocalyptic", "Zombie", "Cybernetic", "Genetic Engineering", "Time Travel", "Parallel World", "Lost World",
                            "Sword and Sorcery", "High Fantasy", "Epic Fantasy", "Dark Fantasy", "Magical Realism", "Grimdark", "Gaslamp Fantasy", "Superhero", "Cosmic Horror", "Gothic Fiction",
                            "Medical Thriller", "Legal Thriller", "Psychological Thriller", "Techno-Thriller", "Military Thriller", "Political Thriller", "Noir", "Neo-Noir", "Hardboiled", "Police Procedural",
                            "Whodunit", "Locked Room Mystery", "Cozy Mystery", "Historical Mystery", "Legal Drama", "Courtroom Drama", "Family Saga", "Epistolary", "Picaresque", "Bildungsroman",
                            "Satire", "Parody", "Allegory", "Fable", "Legend", "Epic Poetry", "Lyric Poetry", "Sonnet", "Haiku", "Free Verse",
                            "Memoir", "Autobiography", "Collective Biography", "Diaries", "Journals", "Letters", "Travelogue", "Essay Collection", "Literary Criticism", "Linguistics",
                            "Ethic", "Metaphysics", "Epistemology", "Logic", "Phenomenology", "Existentialism", "Stoicism", "Analytic Philosophy", "Eastern Philosophy", "Aesthetics",
                            "Cognitive Psychology", "Behavioral Psychology", "Clinical Psychology", "Developmental Psychology", "Social Psychology", "Neuropsychology", "Forensic Psychology", "Counseling", "Psychoanalysis", "Psychotherapy",
                            "Early Childhood Education", "Special Education", "Pedagogy", "Educational Technology", "Higher Education", "Adult Education", "Vocational Training", "Homeschooling", "Standardized Testing", "Curriculum Design",
                            "International Law", "Constitutional Law", "Criminal Law", "Civil Law", "Corporate Law", "Intellectual Property", "Human Rights", "Environmental Law", "Family Law", "Legal Ethics",
                            "Public Health", "Immunology", "Pharmacology", "Anatomy", "Physiology", "Genetics", "Pathology", "Nursing", "Surgery", "Psychiatry",
                            "Macroeconomics", "Microeconomics", "Finance", "Investment", "Real Estate", "Marketing", "Management", "Entrepreneurship", "Human Resources", "Nonprofit Management",
                            "Ancient History", "Medieval History", "Modern History", "Military History", "Economic History", "Cultural History", "Art History", "Women's History", "World War I", "World War II",
                            "Political Theory", "International Relations", "Public Policy", "Comparative Politics", "Urban Planning", "Demographics", "Criminology", "Anthropology", "Archaeology", "Genealogy",
                            "Astrophysics", "Cosmology", "Quantum Mechanics", "Particle Physics", "Nuclear Physics", "Condensed Matter", "Optics", "Electromagnetism", "Relativity", "String Theory",
                            "Inorganic Chemistry", "Organic Chemistry", "Analytical Chemistry", "Physical Chemistry", "Biochemistry", "Molecular Biology", "Cell Biology", "Ecology", "Evolutionary Biology", "Microbiology",
                            "Artificial Intelligence", "Machine Learning", "Data Mining", "Cloud Computing", "Cryptography", "Network Security", "Embedded Systems", "Operating Systems", "Compiler Design", "Human-Computer Interaction",
                            "Civil Engineering", "Mechanical Engineering", "Electrical Engineering", "Chemical Engineering", "Aerospace Engineering", "Robotics", "Material Science", "Structural Engineering", "Hydrology", "Environmental Engineering",
                            "Landscape Architecture", "Interior Design", "Typography", "Fashion Design", "Product Design", "Graphic Design", "Calligraphy", "Sculpture", "Textile Art", "Ceramics",
                            "Classical Music", "Jazz", "Blues", "Rock", "Pop", "Country", "Folk Music", "Electronic Music", "Hip Hop", "World Music",
                            "Cinematography", "Screenwriting", "Animation", "Film Theory", "Acting", "Directing", "Musical Theater", "Radio", "Television", "Digital Media",
                            "Horticulture", "Permaculture", "Conservation", "Wildlife", "Climate Change", "Meteorology", "Oceanography", "Entomology", "Ornithology", "Marine Biology",
                            "Sustainable Living", "Yoga", "Meditation", "Nutrition", "Alternative Medicine", "Physical Therapy", "Dermatology", "Pediatrics", "Geriatrics", "Oncology",
                            "Strategy Games", "Board Games", "Video Games", "Card Games", "RPG", "Chess", "Puzzle", "Outdoor Activity", "Extreme Sports", "Mountaineering",
                            "True Adventure", "Polar Exploration", "Mountaineering", "Navigation", "Survival", "Hunting", "Fishing", "Martial Arts", "Self-Defense", "Crafts",
                            "Knitting", "Woodworking", "Metalworking", "Cooking Techniques", "Baking", "Wine", "Vegetarian", "Vegan", "Bread", "Regional Cuisine"
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

                    ' 3. Security & Audit Columns (V3.5.1)
                    Try
                        Dim cmdUsers As New OleDbCommand("CREATE TABLE Users (UserID AUTOINCREMENT PRIMARY KEY, Username VARCHAR(50) UNIQUE, Password VARCHAR(50), Role VARCHAR(20))", conn)
                        cmdUsers.ExecuteNonQuery()
                        ' Seed Super Admin
                        Dim cmdAdmin As New OleDbCommand("INSERT INTO Users (Username, Password, Role) VALUES ('admin', '2026', 'SuperAdmin')", conn)
                        cmdAdmin.ExecuteNonQuery()
                    Catch
                    End Try

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

                    ' 4. Update Books Table for Relation
                    Try
                        Dim cmdAddCol As New OleDbCommand("ALTER TABLE Books ADD COLUMN CategoryID INTEGER", conn)
                        cmdAddCol.ExecuteNonQuery()
                    Catch
                    End Try

                    ' 5. Sync existing strings to IDs
                    Try
                        ' (Simplified sync for Access OLEDB)
                        Dim syncCmd As New OleDbCommand("UPDATE Books INNER JOIN BookCategories ON Books.Genre = BookCategories.CategoryName SET Books.CategoryID = BookCategories.CatID", conn)
                        syncCmd.ExecuteNonQuery()
                    Catch
                    End Try

            End Using
        Catch ex As Exception
            MessageBox.Show("Initialization Warning: " & ex.Message)
        End Try
    End Sub

    ' ======================================================================
    ' Sub: InitializeComponent
    ' Procedurally registers all graphical controls.
    ' ======================================================================
    Private Sub InitializeComponent()
        Me.Text = "My K Library - 3.6.3"
        Me.MinimumSize = New Size(800, 550)
        Me.Font = New Font("Segoe UI", baseFontSize)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.FormBorderStyle = FormBorderStyle.Sizable
        Me.AutoScroll = False
        Me.MaximizeBox = True

        ' Load resolution preference before sizing
        LoadResolutionMode()
        ApplyResolutionSize()

        If File.Exists("bookic2.ico") Then
            Try : Me.Icon = New Icon("bookic2.ico") : Catch : End Try
        End If

        ' ===== HEADER — 2-row layout (Row1: logo/nav  Row2: action buttons) =====
        Dim clrHeader As Color = Color.FromArgb(44, 44, 62)
        pnlHeader = New Panel() With {.Dock = DockStyle.Top, .Height = 92, .BackColor = clrHeader}
        Me.Controls.Add(pnlHeader)

        ' --- Row 1 (46 px): Logo · Title · Navigation ---
        Dim pnlHdrRow1 As New Panel() With {
            .Dock = DockStyle.Top, .Height = 46, .BackColor = Color.Transparent}
        pnlHeader.Controls.Add(pnlHdrRow1)

        ' CODE-00001 - Application Header & Logo
        If File.Exists("bookic1.png") Then
            Dim picLogo As New PictureBox() With {
                .Size = New Size(34, 34), .Location = New Point(8, 6),
                .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.Transparent}
            picLogo.Image = Image.FromFile("bookic1.png")
            pnlHdrRow1.Controls.Add(picLogo)
        End If

        Dim lblTitle As New Label() With {
            .Text = "My K Library", .Location = New Point(50, 10),
            .AutoSize = True, .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 13, FontStyle.Bold)}
        pnlHdrRow1.Controls.Add(lblTitle)

        ' CODE-00002 - Main Navigation Dropdown
        lblNav = New Label() With {
            .Text = "View:", .AutoSize = True,
            .ForeColor = Color.Silver, .Font = New Font("Segoe UI", 9),
            .Location = New Point(220, 14)}
        pnlHdrRow1.Controls.Add(lblNav)

        cmbNavigation = New ComboBox()
        cmbNavigation.DropDownStyle = ComboBoxStyle.DropDownList
        cmbNavigation.Items.AddRange(New String() {"Welcome Screen", "View Current Books", "Settings"})
        cmbNavigation.SelectedIndex = 0
        cmbNavigation.Location = New Point(258, 11)
        cmbNavigation.Size = New Size(175, 26)
        cmbNavigation.Font = New Font("Segoe UI", 9)
        AddHandler cmbNavigation.SelectedIndexChanged, AddressOf Navigation_Changed
        pnlHdrRow1.Controls.Add(cmbNavigation)

        ' --- Row 2 (46 px): scrollable action + library buttons ---
        Dim pnlHdrRow2 As New Panel() With {
            .Dock = DockStyle.Bottom, .Height = 46,
            .BackColor = Color.FromArgb(35, 35, 52),
            .AutoScroll = True}  ' horizontal scroll auto appears if buttons overflow
        pnlHeader.Controls.Add(pnlHdrRow2)

        Dim flpHeader As New FlowLayoutPanel() With {
            .FlowDirection = FlowDirection.LeftToRight,
            .AutoSize = True, .WrapContents = False,
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Left,
            .Padding = New Padding(0), .Margin = New Padding(0)}
        pnlHdrRow2.Controls.Add(flpHeader)

        Dim MakeHdrBtn = Function(txt As String, bg As Color) As Button
            Dim b As New Button()
            b.Text = txt : b.AutoSize = True
            b.Padding = New Padding(8, 0, 8, 0)
            b.Height = 32 : b.MinimumSize = New Size(0, 32)
            b.FlatStyle = FlatStyle.Flat
            b.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 110)
            b.FlatAppearance.BorderSize = 1
            b.BackColor = bg : b.ForeColor = Color.White
            b.Font = New Font("Segoe UI", 9)
            b.Cursor = Cursors.Hand
            b.Margin = New Padding(3, 7, 3, 7)
            Return b
        End Function

        ' --- Library management (always in row 2) ---
        btnAddLib = MakeHdrBtn("+ Library", Color.FromArgb(41, 128, 185))
        AddHandler btnAddLib.Click, AddressOf AddLibrary
        flpHeader.Controls.Add(btnAddLib)

        btnRenameLib = MakeHdrBtn("✎ Rename", Color.FromArgb(180, 120, 20))
        AddHandler btnRenameLib.Click, AddressOf RenameLibrary
        flpHeader.Controls.Add(btnRenameLib)

        btnDeleteLib = MakeHdrBtn("✖ Del Lib", Color.FromArgb(160, 45, 35))
        AddHandler btnDeleteLib.Click, AddressOf DeleteLibrary
        flpHeader.Controls.Add(btnDeleteLib)

        btnMergeLibs = MakeHdrBtn("🔗 Merge", Color.FromArgb(120, 55, 155))
        AddHandler btnMergeLibs.Click, AddressOf MergeLibraries
        flpHeader.Controls.Add(btnMergeLibs)

        ' Thin separator
        Dim sep As New Panel() With {.Width = 1, .Dock = DockStyle.None, .BackColor = Color.FromArgb(80, 90, 110), .Margin = New Padding(4, 6, 4, 6), .Size = New Size(1, 32)}
        flpHeader.Controls.Add(sep)

        ' CODE-00003 - Global Application Settings & Export
        Dim btnDbSettings As Button = MakeHdrBtn("⚙ DB", Color.FromArgb(100, 60, 60))
        AddHandler btnDbSettings.Click, AddressOf OpenDbSettingsDialog
        flpHeader.Controls.Add(btnDbSettings)

        ' CODE-00004 - Grid Style Config
        Dim btnColOrder As Button = MakeHdrBtn("📑 Style", Color.FromArgb(70, 70, 90))
        AddHandler btnColOrder.Click, AddressOf OpenColumnOrderDialog
        flpHeader.Controls.Add(btnColOrder)

        ' CODE-00005 - Harvest Covers
        Dim btnHarvest As Button = MakeHdrBtn("📥 Harvest", Color.FromArgb(100, 80, 20))
        AddHandler btnHarvest.Click, AddressOf BatchHarvestImages
        flpHeader.Controls.Add(btnHarvest)

        ' CODE-00006 - Export CSV
        Dim btnExport As Button = MakeHdrBtn("📊 Export", Color.FromArgb(20, 80, 120))
        AddHandler btnExport.Click, AddressOf ExportBooksToCsv
        flpHeader.Controls.Add(btnExport)

        ' CODE-00007 - Browser UI Launcher
        Dim btnWebUI As Button = MakeHdrBtn("🌐 Web", Color.FromArgb(30, 100, 60))
        AddHandler btnWebUI.Click, Sub() Process.Start("http://localhost:8080")
        flpHeader.Controls.Add(btnWebUI)

        ' CODE-00008 - Scale Fonts
        btnIncreaseFont = MakeHdrBtn("A+", Color.FromArgb(60, 60, 80))
        btnDecreaseFont = MakeHdrBtn("A-", Color.FromArgb(60, 60, 80))
        AddHandler btnIncreaseFont.Click, AddressOf IncreaseFont
        AddHandler btnDecreaseFont.Click, AddressOf DecreaseFont
        flpHeader.Controls.Add(btnIncreaseFont)
        flpHeader.Controls.Add(btnDecreaseFont)

        ' CODE-00009 - User Management
        If SessionRole = "SuperAdmin" Then
            Dim btnUsers As Button = MakeHdrBtn("👤 Users", Color.FromArgb(140, 100, 0))
            AddHandler btnUsers.Click, Sub()
                Using um As New UserManagerDialog() : um.ShowDialog(Me) : End Using
            End Sub
            flpHeader.Controls.Add(btnUsers)
        End If

        pnlHeader.BringToFront()

        ' ===== CONTENT AREA =====
        pnlBody = New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(245, 245, 240)}
        Me.Controls.Add(pnlBody)
        pnlBody.BringToFront()

        ' ============ WELCOME PANEL ============
        pnlWelcome = New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(245, 246, 250), .AutoScroll = True}
        pnlBody.Controls.Add(pnlWelcome)

        lblProjectName = New Label() With {
            .Text = "My K Library — Dashboard",
            .Font = New Font("Segoe UI", baseFontSize + 10, FontStyle.Bold),
            .AutoSize = True, .Location = New Point(20, 16),
            .ForeColor = Color.FromArgb(28, 40, 60)}
        pnlWelcome.Controls.Add(lblProjectName)

        ' CODE-00010 - Statistical Dashboard Card
        pnlDashboard = New Panel() With {
            .Location = New Point(20, 60), .Size = New Size(860, 240),
            .BackColor = Color.White, .BorderStyle = BorderStyle.None}
        AddHandler pnlDashboard.Paint, AddressOf PaintCard
        pnlWelcome.Controls.Add(pnlDashboard)

        lblTotalLibs = New Label() With {
            .Text = "Active Libraries: ...", .Location = New Point(24, 30),
            .AutoSize = True, .Font = New Font("Segoe UI", baseFontSize + 6, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 40, 60)}
        pnlDashboard.Controls.Add(lblTotalLibs)

        lblTotalBooks = New Label() With {
            .Text = "Total Books: ...", .Location = New Point(24, 75),
            .AutoSize = True, .Font = New Font("Segoe UI", baseFontSize + 6, FontStyle.Bold),
            .ForeColor = Color.FromArgb(39, 174, 96)}
        pnlDashboard.Controls.Add(lblTotalBooks)

        Dim lblTape As New Label() With {
            .Text = "Recently Added:", .Location = New Point(24, 130),
            .AutoSize = True, .Font = New Font("Segoe UI", baseFontSize, FontStyle.Italic),
            .ForeColor = Color.Gray}
        pnlDashboard.Controls.Add(lblTape)

        ' CODE-00011 - Realtime Additions Ticker
        pnlTickerContainer = New Panel() With {
            .Location = New Point(0, 158), .Size = New Size(860, 50),
            .BackColor = Color.FromArgb(28, 40, 60)}
        pnlDashboard.Controls.Add(pnlTickerContainer)

        pnlTickerScroll = New Panel() With {
            .Location = New Point(0, 0), .Size = New Size(5000, 50), .BackColor = Color.Transparent}
        pnlTickerContainer.Controls.Add(pnlTickerScroll)

        timerTicker = New Timer() With {.Interval = 30, .Enabled = True}
        AddHandler timerTicker.Tick, AddressOf Ticker_Tick

        ' CODE-00012 - Welcome Screen Actions
        Dim pnlLibSel As New Panel() With {
            .Location = New Point(20, 315), .BackColor = Color.Transparent, .AutoSize = True}
        pnlWelcome.Controls.Add(pnlLibSel)

        lblChooseLib = New Label() With {
            .Text = "Library:", .Location = New Point(0, 8),
            .AutoSize = True, .Font = New Font("Segoe UI", baseFontSize)}
        pnlLibSel.Controls.Add(lblChooseLib)

        cmbWelcomeLibraries = New ComboBox() With {
            .Location = New Point(80, 4), .Size = New Size(280, 28),
            .DropDownStyle = ComboBoxStyle.DropDownList}
        pnlLibSel.Controls.Add(cmbWelcomeLibraries)

        btnUseLib = CreateAutoBtn("▶  Open Library", Color.FromArgb(39, 174, 96), Color.White)
        btnUseLib.Location = New Point(370, 2)
        AddHandler btnUseLib.Click, AddressOf UseLibrary
        pnlLibSel.Controls.Add(btnUseLib)

        ' Library management buttons moved to header row 2 (always visible).


        ' CODE-00014 - Main View Area Container
        pnlLibrary = New Panel() With {
            .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(245, 246, 250), .Visible = False}
        pnlBody.Controls.Add(pnlLibrary)

        ' *** WinForms Docking Rule: Add DockStyle.Fill child FIRST so it occupies
        ' *** remaining space after top-docked panels claim their strips.
        ' Main Data Layout Container (TableLayoutPanel: 3 columns - grid | editor | covers)
        Dim tlpGridArea As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill, .BackColor = Color.Transparent,
            .ColumnCount = 3, .RowCount = 1}
        tlpGridArea.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        tlpGridArea.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 340.0F))
        tlpGridArea.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 220.0F))
        tlpGridArea.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        pnlLibrary.Controls.Add(tlpGridArea)  ' <-- Must be FIRST

        ' CODE-00019 - Core Database Record DataGridView Component Layer (Column 0)
        dgvBooks = New DataGridView() With {
            .Dock = DockStyle.Fill,
            .BackgroundColor = Color.White,
            .BorderStyle = BorderStyle.None,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            .ReadOnly = True, .AllowUserToAddRows = False,
            .RowHeadersVisible = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .GridColor = Color.FromArgb(220, 222, 230),
            .ColumnHeadersDefaultCellStyle = New DataGridViewCellStyle() With {.BackColor = Color.FromArgb(28,40,60), .ForeColor = Color.White, .Font = New Font("Segoe UI", 9, FontStyle.Bold)}}
        dgvBooks.EnableHeadersVisualStyles = False
        AddHandler dgvBooks.SelectionChanged, AddressOf dgvBooks_SelectionChanged
        tlpGridArea.Controls.Add(dgvBooks, 0, 0)

        ' --- NEW Inline Book Editor (Column 1) ---
        ' CODE-00019b - Inline Book Editor: outer clip container (Column 1)
        ' pnlEditor acts as the visible bordered box and carries Enabled state (lock)
        pnlEditor = New Panel() With {
            .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(250, 251, 253),
            .BorderStyle = BorderStyle.FixedSingle}
        tlpGridArea.Controls.Add(pnlEditor, 1, 0)

        ' Custom-width scrollbar (26 px ≈ 150% of system default 17 px)
        Dim edScrollBar As New VScrollBar() With {
            .Dock = DockStyle.Right, .Width = 26,
            .Minimum = 0, .Maximum = 100, .SmallChange = 30, .LargeChange = 80}
        pnlEditor.Controls.Add(edScrollBar)

        ' Inner content panel - clipped by pnlEditor, scrolled via Top
        pnlEdInner = New Panel() With {
            .AutoScroll = False, .BackColor = Color.FromArgb(250, 251, 253),
            .Location = New Point(0, 0)}
        pnlEditor.Controls.Add(pnlEdInner)

        ' Keep inner panel width in sync whenever the outer panel resizes
        AddHandler pnlEditor.Resize, Sub(s2, e2)
            pnlEdInner.Width = Math.Max(0, pnlEditor.ClientSize.Width - edScrollBar.Width)
            Dim contentH As Integer = pnlEdInner.Height
            Dim visibleH As Integer = pnlEditor.ClientSize.Height
            If contentH > visibleH Then
                edScrollBar.Maximum = (contentH - visibleH) + edScrollBar.LargeChange - 1
                edScrollBar.Enabled = True
            Else
                edScrollBar.Value = 0
                edScrollBar.Enabled = False
                pnlEdInner.Top = 0
            End If
        End Sub

        ' Scroll event: shift inner panel vertically
        AddHandler edScrollBar.Scroll, Sub(s2, e2)
            pnlEdInner.Top = -edScrollBar.Value
        End Sub

        ' Build editor controls inside pnlEdInner
        Dim edY As Integer = 12
        Dim mkEdLbl = Function(t As String) As Label
                          Dim l As New Label() With {.Text = t, .Location = New Point(12, edY), .AutoSize = True, .ForeColor = Color.FromArgb(80, 90, 100), .Font = New Font("Segoe UI", 8, FontStyle.Bold)}
                          pnlEdInner.Controls.Add(l)
                          Return l
                      End Function
        Dim mkEdTxt = Function(h As Integer) As TextBox
                          Dim t As New TextBox() With {.Location = New Point(12, edY + 18), .Width = 290, .Height = h, .Font = New Font("Segoe UI", 10), .BorderStyle = BorderStyle.FixedSingle}
                          pnlEdInner.Controls.Add(t)
                          Return t
                      End Function

        mkEdLbl("Database ID (Read-Only)") : txtEdISBN = mkEdTxt(26) : txtEdISBN.ReadOnly = True : txtEdISBN.BackColor = Color.FromArgb(235, 237, 242) : edY += 48
        mkEdLbl("Book Title *") : txtEdTitle = mkEdTxt(26) : edY += 48
        mkEdLbl("Author") : txtEdAuthor = mkEdTxt(26) : edY += 48

        mkEdLbl("Publication Year") : txtEdYear = mkEdTxt(26) : txtEdYear.Width = 130
        Dim lblPgs As New Label() With {.Text = "Pages", .Location = New Point(154, edY), .AutoSize = True, .ForeColor = Color.FromArgb(80, 90, 100), .Font = New Font("Segoe UI", 8, FontStyle.Bold)}
        pnlEdInner.Controls.Add(lblPgs)
        txtEdPages = New TextBox() With {.Location = New Point(154, edY + 18), .Width = 148, .Height = 26, .Font = New Font("Segoe UI", 10), .BorderStyle = BorderStyle.FixedSingle}
        pnlEdInner.Controls.Add(txtEdPages) : edY += 48

        mkEdLbl("Category / Genre") : txtEdCategory = mkEdTxt(26) : edY += 48
        mkEdLbl("Description / Summary") : txtEdSummary = mkEdTxt(80) : txtEdSummary.Multiline = True : txtEdSummary.ScrollBars = ScrollBars.Vertical : edY += 105
        mkEdLbl("Cover Image URL") : txtEdCoverUrl = mkEdTxt(26) : edY += 48
        mkEdLbl("Back Cover URL") : txtEdBackUrl = mkEdTxt(26) : edY += 48

        ' Editor Action Buttons
        btnEdSave = CreateAutoBtn("💾 Save Changes", Color.FromArgb(39, 174, 96), Color.White)
        btnEdSave.Location = New Point(12, edY) : btnEdSave.Width = 290
        AddHandler btnEdSave.Click, AddressOf InlineSave
        pnlEdInner.Controls.Add(btnEdSave) : edY += 44

        btnEdNew = CreateAutoBtn("➕ New Book (Clear)", Color.FromArgb(41, 128, 185), Color.White)
        btnEdNew.Location = New Point(12, edY) : btnEdNew.Width = 140
        AddHandler btnEdNew.Click, AddressOf InlineNew
        pnlEdInner.Controls.Add(btnEdNew)

        btnEdDelete = CreateAutoBtn("✖ Delete", Color.FromArgb(192, 57, 43), Color.White)
        btnEdDelete.Location = New Point(162, edY) : btnEdDelete.Width = 140
        AddHandler btnEdDelete.Click, AddressOf InlineDelete
        pnlEdInner.Controls.Add(btnEdDelete) : edY += 44

        btnEdSearchOnline = CreateAutoBtn("🌐 Search Online", Color.FromArgb(52, 73, 94), Color.White)
        btnEdSearchOnline.Location = New Point(12, edY) : btnEdSearchOnline.Width = 290
        AddHandler btnEdSearchOnline.Click, AddressOf OpenSearchOnline
        pnlEdInner.Controls.Add(btnEdSearchOnline) : edY += 44

        ' Fix inner panel height to the total content height (content is clipped by outer panel)
        pnlEdInner.Size = New Size(290, edY + 20)

        ' CODE-00020 - Image Previews Dual-Sidebar Block (Cover / Back Graphics) (Column 2)
        Dim pnlCovers As New Panel() With {
            .Dock = DockStyle.Fill, .BackColor = Color.White, .Padding = New Padding(6)}
        tlpGridArea.Controls.Add(pnlCovers, 2, 0)

        lblCoverTitle = New Label() With {.Text = "Cover", .Location = New Point(6, 4), .AutoSize = True, .ForeColor = Color.Gray}
        picBookCover = New PictureBox() With {
            .Location = New Point(6, 22), .Size = New Size(186, 140),
            .BorderStyle = BorderStyle.FixedSingle, .SizeMode = PictureBoxSizeMode.Zoom,
            .BackColor = Color.FromArgb(240, 242, 245)}
        lblBackTitle = New Label() With {.Text = "Back", .Location = New Point(6, 170), .AutoSize = True, .ForeColor = Color.Gray}
        picBookBack = New PictureBox() With {
            .Location = New Point(6, 188), .Size = New Size(186, 140),
            .BorderStyle = BorderStyle.FixedSingle, .SizeMode = PictureBoxSizeMode.Zoom,
            .BackColor = Color.FromArgb(240, 242, 245)}
        pnlCovers.Controls.AddRange(New Control() {lblCoverTitle, picBookCover, lblBackTitle, picBookBack})
        ' Now add Top-docked toolbars in REVERSE visual order (bottom bar added first,
        ' top bar added last) so visual stacking ends up: LibTop / LockRow / BookActions / ScanRow

        ' CODE-00017 - ISBN Hardware Row (Scanner + Lookups)
        pnlScanRow = New FlowLayoutPanel() With {
            .Dock = DockStyle.Top, .Height = 46, .BackColor = Color.FromArgb(235, 237, 240),
            .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False,
            .Padding = New Padding(8, 6, 8, 0)}
        pnlLibrary.Controls.Add(pnlScanRow)

        Dim lblScanPrompt As New Label() With {
            .Text = "ISBN:", .AutoSize = True,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Padding = New Padding(0, 8, 4, 0)}
        pnlScanRow.Controls.Add(lblScanPrompt)

        txtScannerInput = New TextBox() With {
            .Width = 180, .Height = 28, .Font = New Font("Segoe UI", 10),
            .Margin = New Padding(0, 4, 6, 0)}
        pnlScanRow.Controls.Add(txtScannerInput)

        btnScanSearch = CreateAutoBtn("🔍 Lookup", Color.FromArgb(22, 160, 133), Color.White)
        AddHandler btnScanSearch.Click, AddressOf SearchByIsbn
        pnlScanRow.Controls.Add(btnScanSearch)

        ' CODE-00018 - Gemini AI Smart Lookup Button
        Dim btnAiLookup As Button = CreateAutoBtn("🤖 AI Lookup", Color.FromArgb(66, 133, 244), Color.White)
        AddHandler btnAiLookup.Click, AddressOf GeminiIsbnLookup
        pnlScanRow.Controls.Add(btnAiLookup)



        ' CODE-00015 - Write Lock Mechanism
        pnlLockRow = New FlowLayoutPanel() With {
            .Dock = DockStyle.Top, .Height = 44, .BackColor = Color.FromArgb(235, 237, 240),
            .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False,
            .Padding = New Padding(6, 6, 6, 0)}
        pnlLibrary.Controls.Add(pnlLockRow)

        btnToggleLock = CreateAutoBtn("🔒 Unlock Editing", Color.FromArgb(52, 73, 94), Color.White)
        AddHandler btnToggleLock.Click, AddressOf ToggleLock
        pnlLockRow.Controls.Add(btnToggleLock)

        lblSelectedId = New Label() With {
            .Text = "Selection: None", .AutoSize = True,
            .ForeColor = Color.Gray, .Font = New Font("Segoe UI", 9),
            .Padding = New Padding(10, 8, 0, 0)}
        pnlLockRow.Controls.Add(lblSelectedId)

        ' Title bar (topmost toolbar, added last so it appears at top)
        pnlLibTop = New Panel() With {
            .Dock = DockStyle.Top, .Height = 40, .BackColor = Color.White}
        pnlLibrary.Controls.Add(pnlLibTop)

        lblLibraryViewTitle = New Label() With {
            .Text = "Currently Viewing: —",
            .Font = New Font("Segoe UI", baseFontSize + 3, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 40, 60),
            .AutoSize = True,
            .Location = New Point(12, 8)}
        pnlLibTop.Controls.Add(lblLibraryViewTitle)


        ' ============ SETTINGS PANEL ============
        pnlSettings = New Panel() With {
            .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(245, 246, 250), .Visible = False}
        pnlBody.Controls.Add(pnlSettings)
        BuildSettingsPanel()

        ' Persist state events
        AddHandler Me.FormClosing, AddressOf MainForm_FormClosing
        LoadWindowState()

        ' Role-Based UI Enforcement
        If SessionRole = "Viewer" Then
            btnAddLib.Enabled = False : btnDeleteLib.Enabled = False
            btnRenameLib.Enabled = False : btnMergeLibs.Enabled = False
            ' Inline editor buttons disabled for Viewer
            btnEdSave.Enabled = False : btnEdNew.Enabled = False : btnEdDelete.Enabled = False
        ElseIf SessionRole = "Editor" Then
            btnDeleteLib.Enabled = False
            btnEdDelete.Enabled = False
        End If
    End Sub

    ' Paint card shadow/border effect
    Private Sub PaintCard(sender As Object, e As PaintEventArgs)
        Dim p As Panel = DirectCast(sender, Panel)
        e.Graphics.DrawRectangle(New Pen(Color.FromArgb(220, 222, 230), 1), 0, 0, p.Width - 1, p.Height - 1)
    End Sub

    ' Creates a resolution-mode-aware sized auto button
    Private Function CreateAutoBtn(txt As String, bg As Color, fg As Color) As Button
        Dim b As New Button()
        b.Text = txt : b.AutoSize = True
        b.Padding = New Padding(10, 0, 10, 0)
        Dim btnH As Integer = If(resolutionMode = "Small", 30, 36)
        b.Height = btnH : b.MinimumSize = New Size(0, btnH)
        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderSize = 0
        b.BackColor = bg : b.ForeColor = fg
        b.Font = New Font("Segoe UI", If(resolutionMode = "Small", 8.5F, 9.5F))
        b.Cursor = Cursors.Hand
        b.Margin = New Padding(4, 0, 4, 0)
        Return b
    End Function

    ' Builds the Settings panel contents
    Private Sub BuildSettingsPanel()
        pnlSettings.Controls.Clear()
        Dim themeAccent As Color = GetThemeAccent()

        ' ===== HEADER =====
        Dim lblHead As New Label() With {
            .Text = "⚙  Settings",
            .Font = New Font("Segoe UI", baseFontSize + 8, FontStyle.Bold),
            .ForeColor = themeAccent,
            .AutoSize = True, .Location = New Point(24, 20)}
        pnlSettings.Controls.Add(lblHead)

        ' ===== THEME SECTION =====
        Dim lblTheme As New Label() With {
            .Text = "Application Skin / Theme",
            .Font = New Font("Segoe UI", baseFontSize + 1, FontStyle.Bold),
            .ForeColor = themeAccent,
            .AutoSize = True, .Location = New Point(24, 72)}
        pnlSettings.Controls.Add(lblTheme)

        Dim lblThemeDesc As New Label() With {
            .Text = "Choose a color scheme. Changes apply immediately.",
            .Font = New Font("Segoe UI", 9), .ForeColor = Color.Gray,
            .AutoSize = True, .Location = New Point(24, 96)}
        pnlSettings.Controls.Add(lblThemeDesc)

        ' Theme definitions: Name, Emoji, Subtitle, AccentColor
        Dim themes() As String = {"Clean Charcoal", "Dark Mode", "Warm Parchment", "Modern Cool"}
        Dim themeEmoji() As String = {"🧴", "🌙", "📜", "🥶"}
        Dim themeSubs() As String = {
            "Warm neutral tones",
            "Dark body, light text",
            "Cream & amber, book-like",
            "Pure white + deep blue"}
        Dim themeClrs() As Color = {
            Color.FromArgb(82, 115, 160),
            Color.FromArgb(56, 139, 253),
            Color.FromArgb(196, 136, 42),
            Color.FromArgb(25, 118, 210)}

        Dim flpTheme As New FlowLayoutPanel() With {
            .Location = New Point(24, 122), .AutoSize = True,
            .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True,
            .BackColor = Color.Transparent, .Width = 860}
        pnlSettings.Controls.Add(flpTheme)

        For i As Integer = 0 To themes.Length - 1
            Dim tName As String = themes(i)
            Dim tEmoji As String = themeEmoji(i)
            Dim tSub As String = themeSubs(i)
            Dim tClr As Color = themeClrs(i)
            Dim isActive As Boolean = (currentTheme = tName)

            Dim card As New Panel() With {
                .Size = New Size(200, 80),
                .BackColor = If(isActive, tClr, Color.White),
                .Cursor = Cursors.Hand,
                .Margin = New Padding(0, 0, 12, 8)}

            Dim capActive As Boolean = isActive
            Dim capClr As Color = tClr
            AddHandler card.Paint, Sub(s, ev)
                Dim pen As New Pen(If(capActive, capClr, Color.FromArgb(200, 205, 215)), 2)
                ev.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1)
            End Sub

            Dim lblEM As New Label() With {
                .Text = tEmoji & "  " & tName, .AutoSize = True,
                .Font = New Font("Segoe UI", 10, FontStyle.Bold),
                .ForeColor = If(isActive, Color.White, tClr),
                .Location = New Point(10, 10)}
            Dim lblSub As New Label() With {
                .Text = tSub, .AutoSize = False, .Width = 180, .Height = 34,
                .Font = New Font("Segoe UI", 8),
                .ForeColor = If(isActive, Color.FromArgb(230, 230, 230), Color.Gray),
                .Location = New Point(10, 42)}
            card.Controls.Add(lblEM) : card.Controls.Add(lblSub)

            Dim capName As String = tName
            Dim clickSub As EventHandler = Sub(s2, ev2)
                currentTheme = capName
                SaveTheme()
                ApplyTheme()
                BuildSettingsPanel()
            End Sub
            AddHandler card.Click, clickSub
            AddHandler lblEM.Click, clickSub
            AddHandler lblSub.Click, clickSub

            flpTheme.Controls.Add(card)
        Next

        ' ===== SEPARATOR =====
        Dim sep0 As New Panel() With {
            .Location = New Point(24, 230), .Size = New Size(860, 1),
            .BackColor = Color.FromArgb(210, 215, 225)}
        pnlSettings.Controls.Add(sep0)

        ' ===== RESOLUTION SECTION =====
        Dim lblRes As New Label() With {
            .Text = "Display Resolution Mode",
            .Font = New Font("Segoe UI", baseFontSize + 1, FontStyle.Bold),
            .ForeColor = themeAccent,
            .AutoSize = True, .Location = New Point(24, 248)}
        pnlSettings.Controls.Add(lblRes)

        Dim lblResDesc As New Label() With {
            .Text = "Choose the layout density that matches your screen resolution.",
            .Font = New Font("Segoe UI", 9), .ForeColor = Color.Gray,
            .AutoSize = True, .Location = New Point(24, 272)}
        pnlSettings.Controls.Add(lblResDesc)

        Dim resModes() As String = {"Small", "Medium", "Large"}
        Dim resDescs() As String = {
            "≤…1024×768  — compact spacing",
            "1366×768  — balanced (recommended)",
            "≥…1600×900  — spacious layout"}
        Dim resColors() As Color = {Color.FromArgb(231, 76, 60), Color.FromArgb(39, 174, 96), Color.FromArgb(41, 128, 185)}

        Dim flpRes As New FlowLayoutPanel() With {
            .Location = New Point(24, 298), .AutoSize = True,
            .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False,
            .BackColor = Color.Transparent}
        pnlSettings.Controls.Add(flpRes)

        For i As Integer = 0 To 2
            Dim mode As String = resModes(i)
            Dim desc As String = resDescs(i)
            Dim clr As Color = resColors(i)
            Dim isActive As Boolean = (resolutionMode = mode)

            Dim card As New Panel() With {
                .Size = New Size(200, 80),
                .BackColor = If(isActive, clr, Color.White),
                .Cursor = Cursors.Hand,
                .Margin = New Padding(0, 0, 12, 0)}

            Dim capIsActive As Boolean = isActive
            Dim capClr As Color = clr
            AddHandler card.Paint, Sub(s, ev)
                Dim pen As New Pen(If(capIsActive, capClr, Color.FromArgb(200, 205, 215)), 2)
                ev.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1)
            End Sub

            Dim lblMode As New Label() With {
                .Text = mode, .AutoSize = True,
                .Font = New Font("Segoe UI", 13, FontStyle.Bold),
                .ForeColor = If(isActive, Color.White, clr),
                .Location = New Point(12, 10)}
            Dim lblDescLbl As New Label() With {
                .Text = desc, .AutoSize = False, .Width = 176, .Height = 34,
                .Font = New Font("Segoe UI", 8),
                .ForeColor = If(isActive, Color.FromArgb(220, 220, 220), Color.Gray),
                .Location = New Point(12, 38)}
            card.Controls.Add(lblMode) : card.Controls.Add(lblDescLbl)

            Dim capMode As String = mode
            Dim resSub As EventHandler = Sub(s, ev)
                resolutionMode = capMode
                SaveResolutionMode()
                ApplyResolutionSize()
                BuildSettingsPanel()
            End Sub
            AddHandler card.Click, resSub
            AddHandler lblMode.Click, resSub
            AddHandler lblDescLbl.Click, resSub

            flpRes.Controls.Add(card)
        Next
    End Sub

    ' Returns the accent colour used for Settings headings based on the active theme
    Private Function GetThemeAccent() As Color
        Select Case currentTheme
            Case "Dark Mode"       : Return Color.FromArgb(56, 139, 253)
            Case "Warm Parchment" : Return Color.FromArgb(196, 136, 42)
            Case "Modern Cool"    : Return Color.FromArgb(25, 118, 210)
            Case Else             : Return Color.FromArgb(82, 115, 160)  ' Clean Charcoal
        End Select
    End Function

    ' ======================================================================
    ' Theme helpers
    ' ======================================================================
    Private Sub LoadTheme()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT SettingValue FROM Settings WHERE KeyName = 'AppTheme'", conn)
                Dim res = cmd.ExecuteScalar()
                If res IsNot Nothing AndAlso res IsNot DBNull.Value Then currentTheme = res.ToString()
            End Using
        Catch
        End Try
    End Sub

    Private Sub SaveTheme()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                UpsertSetting(conn, "AppTheme", currentTheme)
            End Using
        Catch
        End Try
    End Sub

    Private Sub ApplyTheme()
        ' ---- Resolve palette for chosen theme ----
        Dim headerBG, bodyBG, toolbarBG, editorBG, libTopBG As Color
        Dim gridHdrBG, gridHdrFG, gridBG, gridSelBG As Color
        Dim saveBtnBG, deleteBtnBG, newBtnBG, lockBtnBG As Color
        Dim labelFG, titleFG As Color

        Select Case currentTheme

            Case "Dark Mode"
                headerBG   = Color.FromArgb(26, 26, 46)
                bodyBG     = Color.FromArgb(13, 17, 23)
                toolbarBG  = Color.FromArgb(22, 27, 34)
                editorBG   = Color.FromArgb(22, 27, 34)
                libTopBG   = Color.FromArgb(22, 27, 34)
                gridHdrBG  = Color.FromArgb(33, 38, 45)
                gridHdrFG  = Color.FromArgb(201, 209, 217)
                gridBG     = Color.FromArgb(13, 17, 23)
                gridSelBG  = Color.FromArgb(31, 111, 235)
                saveBtnBG  = Color.FromArgb(35, 134, 54)
                deleteBtnBG= Color.FromArgb(218, 54, 51)
                newBtnBG   = Color.FromArgb(31, 111, 235)
                lockBtnBG  = Color.FromArgb(56, 139, 253)
                labelFG    = Color.FromArgb(201, 209, 217)
                titleFG    = Color.FromArgb(230, 237, 243)

            Case "Warm Parchment"
                headerBG   = Color.FromArgb(92, 61, 46)
                bodyBG     = Color.FromArgb(253, 246, 236)
                toolbarBG  = Color.FromArgb(243, 229, 208)
                editorBG   = Color.FromArgb(255, 249, 240)
                libTopBG   = Color.FromArgb(255, 249, 240)
                gridHdrBG  = Color.FromArgb(107, 76, 59)
                gridHdrFG  = Color.FromArgb(255, 243, 224)
                gridBG     = Color.FromArgb(255, 249, 240)
                gridSelBG  = Color.FromArgb(196, 136, 42)
                saveBtnBG  = Color.FromArgb(196, 136, 42)
                deleteBtnBG= Color.FromArgb(169, 50, 38)
                newBtnBG   = Color.FromArgb(123, 94, 167)
                lockBtnBG  = Color.FromArgb(141, 110, 99)
                labelFG    = Color.FromArgb(78, 52, 46)
                titleFG    = Color.FromArgb(78, 52, 46)

            Case "Modern Cool"
                headerBG   = Color.FromArgb(21, 101, 192)
                bodyBG     = Color.FromArgb(250, 250, 250)
                toolbarBG  = Color.FromArgb(227, 242, 253)
                editorBG   = Color.FromArgb(248, 250, 255)
                libTopBG   = Color.FromArgb(255, 255, 255)
                gridHdrBG  = Color.FromArgb(26, 35, 126)
                gridHdrFG  = Color.White
                gridBG     = Color.White
                gridSelBG  = Color.FromArgb(25, 118, 210)
                saveBtnBG  = Color.FromArgb(25, 118, 210)
                deleteBtnBG= Color.FromArgb(211, 47, 47)
                newBtnBG   = Color.FromArgb(2, 136, 209)
                lockBtnBG  = Color.FromArgb(0, 121, 107)
                labelFG    = Color.FromArgb(26, 35, 126)
                titleFG    = Color.FromArgb(26, 35, 126)

            Case Else  ' Clean Charcoal (default)
                headerBG   = Color.FromArgb(44, 44, 62)
                bodyBG     = Color.FromArgb(245, 245, 240)
                toolbarBG  = Color.FromArgb(235, 235, 232)
                editorBG   = Color.FromArgb(255, 255, 255)
                libTopBG   = Color.FromArgb(255, 255, 255)
                gridHdrBG  = Color.FromArgb(58, 58, 74)
                gridHdrFG  = Color.White
                gridBG     = Color.White
                gridSelBG  = Color.FromArgb(82, 115, 160)
                saveBtnBG  = Color.FromArgb(76, 175, 132)
                deleteBtnBG= Color.FromArgb(192, 57, 43)
                newBtnBG   = Color.FromArgb(61, 126, 191)
                lockBtnBG  = Color.FromArgb(82, 115, 160)
                labelFG    = Color.FromArgb(58, 58, 58)
                titleFG    = Color.FromArgb(44, 44, 62)
        End Select

        ' ---- Apply to panels ----
        If pnlHeader IsNot Nothing Then pnlHeader.BackColor = headerBG
        If pnlBody IsNot Nothing Then
            pnlBody.BackColor = bodyBG
            pnlWelcome.BackColor = bodyBG
            pnlLibrary.BackColor = bodyBG
            pnlSettings.BackColor = bodyBG
        End If
        If pnlScanRow IsNot Nothing Then pnlScanRow.BackColor = toolbarBG
        If pnlLockRow IsNot Nothing Then pnlLockRow.BackColor = toolbarBG
        If pnlLibTop IsNot Nothing Then pnlLibTop.BackColor = libTopBG
        If pnlEditor IsNot Nothing Then pnlEditor.BackColor = editorBG
        If pnlEdInner IsNot Nothing Then pnlEdInner.BackColor = editorBG

        ' ---- Grid colours ----
        If dgvBooks IsNot Nothing Then
            dgvBooks.BackgroundColor = gridBG
            dgvBooks.DefaultCellStyle.BackColor = gridBG
            dgvBooks.DefaultCellStyle.ForeColor = labelFG
            dgvBooks.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(
                Math.Min(255, gridBG.R - 8), Math.Min(255, gridBG.G - 8), Math.Min(255, gridBG.B - 8))
            dgvBooks.ColumnHeadersDefaultCellStyle.BackColor = gridHdrBG
            dgvBooks.ColumnHeadersDefaultCellStyle.ForeColor = gridHdrFG
            dgvBooks.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9, FontStyle.Bold)
            dgvBooks.GridColor = Color.FromArgb(210, 212, 218)
        End If

        ' ---- Buttons ----
        If btnEdSave IsNot Nothing Then btnEdSave.BackColor = saveBtnBG
        If btnEdDelete IsNot Nothing Then btnEdDelete.BackColor = deleteBtnBG
        If btnEdNew IsNot Nothing Then btnEdNew.BackColor = newBtnBG
        If btnEdSearchOnline IsNot Nothing Then btnEdSearchOnline.BackColor = Color.FromArgb(82, 95, 110)
        If btnToggleLock IsNot Nothing Then
            If isLocked Then
                btnToggleLock.BackColor = lockBtnBG
            Else
                btnToggleLock.BackColor = saveBtnBG
            End If
        End If

        ' ---- Labels ----
        If lblLibraryViewTitle IsNot Nothing Then lblLibraryViewTitle.ForeColor = titleFG
        If lblSelectedId IsNot Nothing Then lblSelectedId.ForeColor = Color.FromArgb(
            labelFG.R \ 2 + 90, labelFG.G \ 2 + 90, labelFG.B \ 2 + 90)

        Me.Refresh()
    End Sub


    ' Resolution helpers
    Private Sub LoadResolutionMode()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT SettingValue FROM Settings WHERE KeyName = 'ResolutionMode'", conn)
                Dim res = cmd.ExecuteScalar()
                If res IsNot Nothing AndAlso res IsNot DBNull.Value Then resolutionMode = res.ToString()
            End Using
        Catch
        End Try
    End Sub

    Private Sub SaveResolutionMode()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                UpsertSetting(conn, "ResolutionMode", resolutionMode)
            End Using
        Catch
        End Try
    End Sub

    Private Sub ApplyResolutionSize()
        Select Case resolutionMode
            Case "Small"
                If Me.WindowState = FormWindowState.Normal Then Me.Size = New Size(1024, 700)
                Me.Font = New Font("Segoe UI", Math.Max(baseFontSize - 1, 8))
            Case "Large"
                If Me.WindowState = FormWindowState.Normal Then Me.Size = New Size(1600, 950)
                Me.Font = New Font("Segoe UI", baseFontSize + 1)
            Case Else ' Medium
                If Me.WindowState = FormWindowState.Normal Then Me.Size = New Size(1280, 800)
                Me.Font = New Font("Segoe UI", baseFontSize)
        End Select
    End Sub

    ' Helper method to standardise button generation styles
    Private Function CreateFlatButton(text As String, x As Integer, y As Integer, w As Integer, h As Integer) As Button
        Dim btn As New Button()
        btn.Text = text
        btn.Location = New Point(x, y)
        btn.Size = New Size(w, h)
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.BorderColor = Color.LightGray
        btn.BackColor = Color.WhiteSmoke
        btn.Cursor = Cursors.Hand
        Return btn
    End Function

    '============= UI EVENTS (NAVIGATION) ===============

    Private Sub Navigation_Changed(sender As Object, e As EventArgs)
        SwitchView(cmbNavigation.SelectedItem.ToString())
    End Sub

    Private Sub SwitchView(viewName As String)
        pnlWelcome.Visible = False
        pnlLibrary.Visible = False
        pnlSettings.Visible = False

        Select Case viewName
            Case "Welcome Screen"
                pnlWelcome.Visible = True
                pnlWelcome.BringToFront()
                LoadLibraries()
                UpdateDashboard()
            Case "View Current Books"
                pnlLibrary.Visible = True
                pnlLibrary.BringToFront()
                lblLibraryViewTitle.Text = "Currently Viewing: " & currentLibrary
                LoadBooks()
                If Not isLocked Then
                    isLocked = True
                    UpdateUIState()
                End If

            Case "Settings"
                pnlSettings.Visible = True
                pnlSettings.BringToFront()
        End Select
    End Sub

    ' Opens whichever library the Welcome dropdown is set to
    Private Sub UseLibrary(sender As Object, e As EventArgs)
        If cmbWelcomeLibraries.SelectedItem IsNot Nothing Then
            currentLibrary = cmbWelcomeLibraries.SelectedItem.ToString()
            cmbNavigation.SelectedItem = "View Current Books" 
        End If
    End Sub

    '============= UI EVENTS (FONT SIZING) ===============
    Private Sub IncreaseFont(sender As Object, e As EventArgs)
        If baseFontSize < 24.0F Then
            baseFontSize += 1.0F
            ApplyFont()
        End If
    End Sub

    Private Sub DecreaseFont(sender As Object, e As EventArgs)
        If baseFontSize > 6.0F Then
            baseFontSize -= 1.0F
            ApplyFont()
        End If
    End Sub

    Private Sub ApplyFont()
        Me.Font = New Font("Segoe UI", baseFontSize)
        lblProjectName.Font = New Font("Segoe UI", baseFontSize + 10, FontStyle.Bold)
        lblLibraryViewTitle.Font = New Font("Segoe UI", baseFontSize + 3, FontStyle.Bold)
        lblChooseLib.Font = New Font("Segoe UI", baseFontSize)
    End Sub

    '============= UI EVENTS (SECURITY) ===============

    ' Triggers the admin123 password payload via interacting heavily with InputBox
    Private Sub ToggleLock(sender As Object, e As EventArgs)
        If isLocked Then
            Dim pwd As String = Interaction.InputBox("Enter password to unlock adding/editing:", "Unlock Library", "")
            If pwd = "admin" Then ' Emergency Unlock for Admin
                isLocked = False
            ElseIf Not String.IsNullOrEmpty(pwd) Then
                MessageBox.Show("Incorrect password.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Else
            isLocked = True
        End If
        UpdateUIState()
    End Sub

    ' (Redundant method removed - see Shared version at end of class)

    ' Migration Logic: Convert all existing Web URLs to Local Images
    Private Sub BatchHarvestImages()
        Dim count As Integer = 0
        Using conn As New OleDbConnection(connString)
            conn.Open()
            Dim cmd As New OleDbCommand("SELECT [ID], [ISBN], [CoverPath], [BackPath] FROM Books WHERE [CoverPath] LIKE 'http%' OR [BackPath] LIKE 'http%'", conn)
            Using reader As OleDbDataReader = cmd.ExecuteReader()
                Dim updates As New List(Of String())
                While reader.Read()
                    Dim id As String = reader("ID").ToString()
                    Dim isbn As String = reader("ISBN").ToString()
                    Dim cp As String = reader("CoverPath").ToString()
                    Dim bp As String = reader("BackPath").ToString()
                    
                    Dim newCp As String = cp
                    Dim newBp As String = bp
                    
                    If cp.StartsWith("http") Then newCp = EnsureLocalImage(cp, isbn, "cover")
                    If bp.StartsWith("http") Then newBp = EnsureLocalImage(bp, isbn, "back")
                    
                    updates.Add(New String() {id, newCp, newBp})
                End While
                reader.Close()

                For Each up In updates
                    Dim upCmd As New OleDbCommand("UPDATE Books SET [CoverPath]=@cp, [BackPath]=@bp WHERE [ID]=@id", conn)
                    upCmd.Parameters.AddWithValue("@cp", up(1))
                    upCmd.Parameters.AddWithValue("@bp", up(2))
                    upCmd.Parameters.AddWithValue("@id", up(0))
                    upCmd.ExecuteNonQuery()
                    count += 1
                Next
            End Using
        End Using
        
        LoadBooks()
        MessageBox.Show("Finished! Harvested images for " & count & " books.", "Image Harvest")
    End Sub

    ' Export current library data to a CSV report
    Private Sub ExportBooksToCsv()
        Dim sfd As New SaveFileDialog()
        sfd.Filter = "CSV Files (*.csv)|*.csv"
        sfd.FileName = "Library_Report_" & DateTime.Now.ToString("yyyyMMdd") & ".csv"
        
        If sfd.ShowDialog() = DialogResult.OK Then
            Try
                Using sw As New StreamWriter(sfd.FileName, False, System.Text.Encoding.UTF8)
                    ' Write Header
                    sw.WriteLine("ID,Title,Author,ISBN,Publisher,Year,Pages,Series,Genre,CoAuthors,Description,Language,Weight,Dimensions,LCCN,OCLC,Subjects,CoverPath,BackPath")
                    
                    Using conn As New OleDbConnection(connString)
                        conn.Open()
                        Dim cmd As New OleDbCommand("SELECT [ID], [Title], [Author], [ISBN], [Publisher], [PubYear], [Pages], [Series], [Genre], [CoAuthors], [Description], [Language], [Weight], [Dimensions], [LCCN], [OCLC], [Subjects], [CoverPath], [BackPath] FROM Books ORDER BY [Title] ASC", conn)
                        Using reader = cmd.ExecuteReader()
                            While reader.Read()
                                Dim fields As New List(Of String)
                                For i As Integer = 0 To reader.FieldCount - 1
                                    Dim val As String = If(reader(i) Is DBNull.Value, "", reader(i).ToString())
                                    ' Escape CSV (quotes and commas)
                                    If val.Contains(",") Or val.Contains("""") Or val.Contains(Environment.NewLine) Then
                                        val = """" & val.Replace("""", """""") & """"
                                    End If
                                    fields.Add(val)
                                Next
                                sw.WriteLine(String.Join(",", fields))
                            End While
                        End Using
                    End Using
                End Using
                MessageBox.Show("Report exported successfully to: " & sfd.FileName, "Export Success")
            Catch ex As Exception
                MessageBox.Show("Export failed: " & ex.Message, "Export Error")
            End Try
        End If
    End Sub

    Private Sub UpdateUIState()
        If isLocked Then
            btnToggleLock.Text = "🔒 Unlock Editing"
            btnToggleLock.BackColor = Color.FromArgb(52, 73, 94)
            If pnlEditor IsNot Nothing Then pnlEditor.Enabled = False
        Else
            btnToggleLock.Text = "🔓 Lock Editing"
            btnToggleLock.BackColor = Color.FromArgb(39, 174, 96)
            If pnlEditor IsNot Nothing Then pnlEditor.Enabled = True
        End If
    End Sub

    '============= GRADIENT BACKGROUND ==============

    ' Generates a new random pleasant gradient on every launch
    ' and paints it on the welcome panel background.
    Private _gradColor1 As Color = Color.FromArgb(245, 246, 250)
    Private _gradColor2 As Color = Color.FromArgb(210, 225, 245)

    Private Sub InitRandomGradient()
        Dim rnd As New Random()
        ' Pick two harmonious colors from 255-palette quadrants
        Dim h1 As Integer = rnd.Next(0, 360)
        Dim h2 As Integer = (h1 + 40 + rnd.Next(0, 80)) Mod 360
        _gradColor1 = HsvToColor(h1, 0.08 + rnd.NextDouble() * 0.07, 0.97)
        _gradColor2 = HsvToColor(h2, 0.12 + rnd.NextDouble() * 0.08, 0.92)
        ' Re-paint welcome panel
        pnlWelcome.Invalidate()
    End Sub

    Private Function HsvToColor(h As Double, s As Double, v As Double) As Color
        Dim hi As Integer = CInt(Math.Floor(h / 60)) Mod 6
        Dim f As Double = h / 60 - Math.Floor(h / 60)
        Dim p As Double = v * (1 - s)
        Dim q As Double = v * (1 - f * s)
        Dim t As Double = v * (1 - (1 - f) * s)
        Dim r, g, b As Double
        Select Case hi
            Case 0 : r = v : g = t : b = p
            Case 1 : r = q : g = v : b = p
            Case 2 : r = p : g = v : b = t
            Case 3 : r = p : g = q : b = v
            Case 4 : r = t : g = p : b = v
            Case Else : r = v : g = p : b = q
        End Select
        Return Color.FromArgb(CInt(r * 255), CInt(g * 255), CInt(b * 255))
    End Function

    Private Sub pnlWelcome_Paint(sender As Object, e As PaintEventArgs)
        Dim rect As New Rectangle(0, 0, pnlWelcome.Width, pnlWelcome.Height)
        Using br As New System.Drawing.Drawing2D.LinearGradientBrush(
            rect, _gradColor1, _gradColor2,
            System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal)
            e.Graphics.FillRectangle(br, rect)
        End Using
    End Sub

    '============= DASHBOARD STATISTICS ==============

    Private Sub UpdateDashboard()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                ' 1. Libraries Count
                Dim cmdL As New OleDbCommand("SELECT COUNT(*) FROM Libraries", conn)
                lblTotalLibs.Text = "Active Libraries: " & cmdL.ExecuteScalar().ToString()

                ' 2. Books Count
                Dim cmdB As New OleDbCommand("SELECT COUNT(*) FROM Books", conn)
                lblTotalBooks.Text = "Total Books Tracked: " & cmdB.ExecuteScalar().ToString()

                ' 3. Ticker Items
                pnlTickerScroll.Controls.Clear()
                Dim cmdT As New OleDbCommand("SELECT TOP 30 ID, Title, Author FROM Books ORDER BY ID DESC", conn)
                Dim reader As OleDbDataReader = cmdT.ExecuteReader()
                Dim xPos As Integer = 50
                While reader.Read()
                    Dim btnItem As New Button()
                    btnItem.Text = reader("Title").ToString() & " (" & reader("Author").ToString() & ")"
                    btnItem.Tag = reader("ID")
                    btnItem.AutoSize = True
                    btnItem.FlatStyle = FlatStyle.Flat
                    btnItem.FlatAppearance.BorderSize = 0
                    btnItem.ForeColor = Color.White
                    btnItem.Font = New Font("Segoe UI", baseFontSize, FontStyle.Bold)
                    btnItem.BackColor = Color.Transparent
                    btnItem.Location = New Point(xPos, 15)
                    btnItem.Cursor = Cursors.Hand
                    AddHandler btnItem.Click, AddressOf TickerItem_Click
                    pnlTickerScroll.Controls.Add(btnItem)
                    xPos += btnItem.Width + 40
                End While
                pnlTickerScroll.Width = Math.Max(xPos + 1000, 5000)
                pnlTickerScroll.Left = 0
            End Using
        Catch
        End Try
    End Sub

    Private Sub Ticker_Tick(sender As Object, e As EventArgs)
        If pnlWelcome.Visible Then
            pnlTickerScroll.Left -= 2
            ' If it scrolled all the way, reset
            If pnlTickerScroll.Left < -pnlTickerScroll.Width + 1000 Then
                pnlTickerScroll.Left = 1000
            End If
        End If
    End Sub

    Private Sub TickerItem_Click(sender As Object, e As EventArgs)
        Dim btn As Button = DirectCast(sender, Button)
        Dim id As Integer = Convert.ToInt32(btn.Tag)

        ' Navigate to View Books and select this book in the grid
        cmbNavigation.SelectedItem = "View Current Books"

        ' Find and select the row in the grid
        For Each row As DataGridViewRow In dgvBooks.Rows
            If row.Cells("ID").Value IsNot Nothing AndAlso Convert.ToInt32(row.Cells("ID").Value) = id Then
                dgvBooks.ClearSelection()
                row.Selected = True
                dgvBooks.FirstDisplayedScrollingRowIndex = row.Index
                Exit For
            End If
        Next
    End Sub

    '============= LIBRARY ROOT MANAGEMENT ===============

    ' Queries all Library names into Welcome ComboBox
    Private Sub LoadLibraries()
        cmbWelcomeLibraries.Items.Clear()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmd As New OleDbCommand("SELECT LibName FROM Libraries ORDER BY LibName", conn)
                Dim reader As OleDbDataReader = cmd.ExecuteReader()
                While reader.Read()
                    cmbWelcomeLibraries.Items.Add(reader("LibName").ToString())
                End While
            End Using
        Catch ex As Exception
        End Try
        If cmbWelcomeLibraries.Items.Count > 0 Then
            Dim idx As Integer = cmbWelcomeLibraries.Items.IndexOf(currentLibrary)
            If idx >= 0 Then
                cmbWelcomeLibraries.SelectedIndex = idx
            Else
                cmbWelcomeLibraries.SelectedIndex = 0
                currentLibrary = cmbWelcomeLibraries.SelectedItem.ToString()
            End If
        End If
    End Sub

    ' Triggers INSERT query to Libraries collection
    Private Sub AddLibrary(sender As Object, e As EventArgs)
        Dim newLib As String = Interaction.InputBox("Enter the name of the new library:", "New Library", "")
        If Not String.IsNullOrWhiteSpace(newLib) Then
            Try
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("INSERT INTO Libraries (LibName) VALUES (?)", conn)
                    cmd.Parameters.AddWithValue("?", newLib.Trim())
                    cmd.ExecuteNonQuery()
                End Using
                currentLibrary = newLib.Trim()
                LoadLibraries()
                MessageBox.Show("Library created!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show("Could not create library. It might already exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End If
    End Sub

    ' Renames target library and migrates ALL books to the new identifier
    Private Sub RenameLibrary(sender As Object, e As EventArgs)
        If cmbWelcomeLibraries.SelectedItem Is Nothing Then Return
        Dim oldLib As String = cmbWelcomeLibraries.SelectedItem.ToString()
        Dim newLib As String = Interaction.InputBox("Enter new name for '" & oldLib & "':", "Rename", oldLib)
        If Not String.IsNullOrWhiteSpace(newLib) AndAlso newLib <> oldLib Then
            Try
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("UPDATE Libraries SET LibName = ? WHERE LibName = ?", conn)
                    cmd.Parameters.AddWithValue("?", newLib.Trim())
                    cmd.Parameters.AddWithValue("?", oldLib)
                    cmd.ExecuteNonQuery()

                    ' Crucial: Migrates Book references via UPDATE
                    Dim cmd2 As New OleDbCommand("UPDATE Books SET LibraryName = ? WHERE LibraryName = ?", conn)
                    cmd2.Parameters.AddWithValue("?", newLib.Trim())
                    cmd2.Parameters.AddWithValue("?", oldLib)
                    cmd2.ExecuteNonQuery()
                End Using
                If currentLibrary = oldLib Then currentLibrary = newLib.Trim()
                LoadLibraries()
            Catch ex As Exception
                MessageBox.Show("Rename failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    ' Cascades delete: Deletes root Library followed by all books containing Library identifier
    Private Sub DeleteLibrary(sender As Object, e As EventArgs)
        If cmbWelcomeLibraries.SelectedItem Is Nothing Then Return
        Dim libName As String = cmbWelcomeLibraries.SelectedItem.ToString()
        If MessageBox.Show("Are you completely sure you want to DELETE '" & libName & "' and ALL its books?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            Try
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("DELETE FROM Libraries WHERE LibName = ?", conn)
                    cmd.Parameters.AddWithValue("?", libName)
                    cmd.ExecuteNonQuery()

                    Dim cmd2 As New OleDbCommand("DELETE FROM Books WHERE LibraryName = ?", conn)
                    cmd2.Parameters.AddWithValue("?", libName)
                    cmd2.ExecuteNonQuery()
                End Using
                LoadLibraries()
            Catch ex As Exception
                MessageBox.Show("Delete failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    ' Allows merging Source Library into Target Library while cascading Book identifiers over securely
    Private Sub MergeLibraries(sender As Object, e As EventArgs)
        If cmbWelcomeLibraries.SelectedItem Is Nothing Then Return
        Dim sourceLib As String = cmbWelcomeLibraries.SelectedItem.ToString()
        Dim targetLib As String = Interaction.InputBox("Merging '" & sourceLib & "' into another library." & vbCrLf & vbCrLf & "Enter the EXACT name of the target library (Source will be emptied and deleted):", "Merge Libraries", "")
        If Not String.IsNullOrWhiteSpace(targetLib) AndAlso targetLib <> sourceLib Then
            Try
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim chkCmd As New OleDbCommand("SELECT COUNT(*) FROM Libraries WHERE LibName = ?", conn)
                    chkCmd.Parameters.AddWithValue("?", targetLib.Trim())
                    If Convert.ToInt32(chkCmd.ExecuteScalar()) = 0 Then
                        MessageBox.Show("Target library does not exist. Check spelling.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                        Return
                    End If

                    Dim upCmd As New OleDbCommand("UPDATE Books SET LibraryName = ? WHERE LibraryName = ?", conn)
                    upCmd.Parameters.AddWithValue("?", targetLib.Trim())
                    upCmd.Parameters.AddWithValue("?", sourceLib)
                    upCmd.ExecuteNonQuery()

                    Dim delCmd As New OleDbCommand("DELETE FROM Libraries WHERE LibName = ?", conn)
                    delCmd.Parameters.AddWithValue("?", sourceLib)
                    delCmd.ExecuteNonQuery()
                End Using
                MessageBox.Show("Merge Successful!", "Merged", MessageBoxButtons.OK, MessageBoxIcon.Information)
                If currentLibrary = sourceLib Then currentLibrary = targetLib.Trim()
                LoadLibraries()
            Catch ex As Exception
                MessageBox.Show("Merge failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    '================= STATE PERSISTENCE ==================

    ' Saves current window geometry and state to Settings table
    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                UpsertSetting(conn, "FormMaximized", (Me.WindowState = FormWindowState.Maximized).ToString())
                
                ' Only save size/location if normal (not minimized or maximized)
                If Me.WindowState = FormWindowState.Normal Then
                    UpsertSetting(conn, "FormWidth", Me.Width.ToString())
                    UpsertSetting(conn, "FormHeight", Me.Height.ToString())
                    UpsertSetting(conn, "FormX", Me.Location.X.ToString())
                    UpsertSetting(conn, "FormY", Me.Location.Y.ToString())
                Else
                    UpsertSetting(conn, "FormWidth", Me.RestoreBounds.Width.ToString())
                    UpsertSetting(conn, "FormHeight", Me.RestoreBounds.Height.ToString())
                    UpsertSetting(conn, "FormX", Me.RestoreBounds.Location.X.ToString())
                    UpsertSetting(conn, "FormY", Me.RestoreBounds.Location.Y.ToString())
                End If
            End Using
        Catch
        End Try
    End Sub

    ' Applies last known geometry from Settings table
    ' ============ EMBEDDED WEB SERVER (V3.4.0) ============
    Private listener As HttpListener
    Private Sub StartWebServer()
        Try
            listener = New HttpListener()
            listener.Prefixes.Add("http://localhost:8080/")
            listener.Start()
            While listener.IsListening
                Dim ctx = listener.GetContext()
                Task.Run(Sub() HandleWebRequest(ctx))
            End While
        Catch ex As Exception
            ' Silent fail if port 8080 occupied
        End Try
    End Sub

    Private Sub HandleWebRequest(ctx As HttpListenerContext)
        Dim req = ctx.Request
        Dim resp = ctx.Response
        Dim rawPath = req.Url.AbsolutePath.ToLower()

        Try
            If rawPath = "/" Or rawPath = "/index.html" Then
                ' Serve stunning Ivory Web Dashboard
                Dim html = GetWebDashboardHtml()
                Dim buf = System.Text.Encoding.UTF8.GetBytes(html)
                ctx.Response.ContentEncoding = System.Text.Encoding.UTF8
                resp.ContentType = "text/html; charset=UTF-8"
                resp.ContentLength64 = buf.Length
                resp.OutputStream.Write(buf, 0, buf.Length)
            ElseIf rawPath = "/api/books" Then
                ' JSON API for books
                Dim json = GetBooksJson()
                Dim buf = System.Text.Encoding.UTF8.GetBytes(json)
                ctx.Response.ContentEncoding = System.Text.Encoding.UTF8
                resp.ContentType = "application/json; charset=UTF-8"
                resp.ContentLength64 = buf.Length
                resp.OutputStream.Write(buf, 0, buf.Length)
            ElseIf rawPath = "/api/stats" Then
                ' JSON API for statistics
                Dim json = String.Format("{{""libraries"":""{0}"",""books"":""{1}""}}", lblTotalLibs.Text, lblTotalBooks.Text)
                Dim buf = System.Text.Encoding.UTF8.GetBytes(json)
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
                Dim cmd As New OleDbCommand("SELECT TOP 50 Title, Author, ISBN FROM Books ORDER BY ID DESC", conn)
                Dim reader = cmd.ExecuteReader()
                Dim sb As New System.Text.StringBuilder("[")
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
        Return "<!DOCTYPE html><html><head><meta charset='UTF-8'><title>My K Library Dashboard</title>" &
               "<style>" &
               "body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #FFFFF0; margin: 0; padding: 20px; color: #333; }" &
               ".card { background: rgba(255, 255, 255, 0.8); backdrop-filter: blur(12px); border-radius: 20px; padding: 30px; box-shadow: 0 10px 40px rgba(0,0,0,0.08); border: 1px solid #fff; margin-bottom: 25px; }" &
               "h1 { font-size: 2.8rem; margin: 0; color: #2c3e50; font-weight: 800; }" &
               ".search-bar { width: 100%; padding: 15px; border-radius: 12px; border: 1px solid #ddd; font-size: 1.1rem; margin: 20px 0; outline: none; transition: 0.3s; }" &
               ".search-bar:focus { border-color: #27ae60; box-shadow: 0 0 15px rgba(39, 174, 96, 0.2); }" &
               ".book-item { display: flex; align-items: center; padding: 15px; border-bottom: 1px solid #f0f0f0; transition: transform 0.2s; }" &
               ".book-item:hover { transform: translateX(10px); background: rgba(0,0,0,0.01); }" &
               ".book-icon { font-size: 2rem; margin-right: 20px; color: #27ae60; }" &
               ".audit-tag { font-size: 0.75rem; background: #eee; padding: 2px 8px; border-radius: 4px; color: #666; }" &
               "</style></head><body>" &
               "<div class='card'><h1>📂 My K Library</h1><p>V3.5.1 - Secured & Audited Web Portal</p></div>" &
               "<input type='text' id='search' class='search-bar' placeholder='Search by title, author or isbn...' onkeyup='filterBooks()'>" &
               "<div class='card'><h3>Recently Added</h3><div id='bookList'>Initializing Archive...</div></div>" &
               "<script>" &
               "let allBooks = [];" &
               "async function load() {" &
               "  allBooks = await fetch('/api/books').then(r => r.json());" &
               "  render(allBooks);" &
               "}" &
               "function render(data) {" &
               "  const list = document.getElementById('bookList');" &
               "  if (data.length === 0) { list.innerHTML = '<i>No records found.</i>'; return; }" &
               "  list.innerHTML = data.map(b => `<div class='book-item'><div class='book-icon'>📕</div><div><b>${b.title}</b><br><small>${b.author}</small> <span class='audit-tag'>Added by: ${b.addedby || 'System'}</span></div></div>`).join('');" &
               "}" &
               "function filterBooks() {" &
               "  const q = document.getElementById('search').value.toLowerCase();" &
               "  const filtered = allBooks.filter(b => b.title.toLowerCase().includes(q) || b.author.toLowerCase().includes(q) || b.isbn.includes(q));" &
               "  render(filtered);" &
               "}" &
               "load();" &
               "</script></body></html>"
    End Function

    Private Sub LoadWindowState()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim w As String = GetSetting(conn, "FormWidth")
                Dim h As String = GetSetting(conn, "FormHeight")
                Dim x As String = GetSetting(conn, "FormX")
                Dim y As String = GetSetting(conn, "FormY")
                Dim max As String = GetSetting(conn, "FormMaximized")

                If Not String.IsNullOrEmpty(w) AndAlso Not String.IsNullOrEmpty(h) Then
                    Me.Size = New Size(Val(w), Val(h))
                End If
                If Not String.IsNullOrEmpty(x) AndAlso Not String.IsNullOrEmpty(y) Then
                    Me.Location = New Point(Val(x), Val(y))
                End If
                If max = "True" Then
                    Me.WindowState = FormWindowState.Maximized
                End If
            End Using
        Catch
        End Try
    End Sub

    Private Sub UpsertSetting(conn As OleDbConnection, key As String, val As String)
        Try
            ' Try update
            Dim cmdU As New OleDbCommand("UPDATE Settings SET SettingValue = ? WHERE KeyName = ?", conn)
            cmdU.Parameters.AddWithValue("?", val)
            cmdU.Parameters.AddWithValue("?", key)
            If cmdU.ExecuteNonQuery() = 0 Then
                ' If no update, insert
                Dim cmdI As New OleDbCommand("INSERT INTO Settings (KeyName, SettingValue) VALUES (?, ?)", conn)
                cmdI.Parameters.AddWithValue("?", key)
                cmdI.Parameters.AddWithValue("?", val)
                cmdI.ExecuteNonQuery()
            End If
        Catch
        End Try
    End Sub

    Private Function GetSetting(conn As OleDbConnection, key As String) As String
        Try
            Dim cmd As New OleDbCommand("SELECT SettingValue FROM Settings WHERE KeyName = ?", conn)
            cmd.Parameters.AddWithValue("?", key)
            Dim res As Object = cmd.ExecuteScalar()
            If res Is Nothing OrElse res Is DBNull.Value Then Return ""
            Return res.ToString()
        Catch
            Return ""
        End Try
    End Function

    '============= BOOKS DATA MANAGEMENT ===============

    ' Queries books table conditionally matched to Active currentLibrary string
    Private Sub LoadBooks()
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                ' Explicit list of columns to avoid pulling legacy [Genre] string field
                Dim cmd As New OleDbCommand("SELECT B.[ID], B.[Title], B.[Author], B.[Author2], B.[Author3], B.[Author4], B.[Author5], B.[Author6], B.[Author7], B.[Author8], B.[Author9], B.[Author10], B.[CoAuthors], B.[Pages], B.[PubYear], B.[Publisher], B.[ISBN], B.[CopyNum], B.[Series], B.[CoverPath], B.[BackPath], B.[Description], B.[Language], B.[Weight], B.[Dimensions], B.[LCCN], B.[OCLC], B.[Subjects], B.[AddedBy], B.[EditedBy], C.CategoryName AS [Book Category], B.CategoryID " &
                                          "FROM Books AS B LEFT JOIN BookCategories AS C ON B.CategoryID = C.CatID " &
                                          "WHERE B.LibraryName = ? ORDER BY B.ID DESC", conn)
                cmd.Parameters.AddWithValue("?", currentLibrary)
                
                Dim adapter As New OleDbDataAdapter(cmd)
                Dim dt As New System.Data.DataTable()
                adapter.Fill(dt)
                dgvBooks.DataSource = dt
            End Using
            LoadGridLayout()
        Catch ex As Exception
            MessageBox.Show("Error loading books: " & ex.Message)
        End Try
    End Sub
    
    ' Selection Event Handler for Grid
    Private Sub dgvBooks_SelectionChanged(sender As Object, e As EventArgs)
        If dgvBooks.SelectedRows.Count > 0 Then
            Dim row = dgvBooks.SelectedRows(0)
            Dim bookId As String = SafeGetVal(row, "ID", "")
            Dim bookTitle As String = SafeGetVal(row, "Title", "Unknown")
            lblSelectedId.Text = "Selected ID: " & bookId & " - " & bookTitle
            
            ' Populate Inline Editor (correct column names from LoadBooks query)
            txtEdISBN.Text = bookId
            txtEdTitle.Text = bookTitle
            txtEdAuthor.Text = SafeGetVal(row, "Author", "")
            txtEdYear.Text = SafeGetVal(row, "PubYear", "")
            txtEdPages.Text = SafeGetVal(row, "Pages", "")
            txtEdCategory.Text = SafeGetVal(row, "Book Category", "")
            txtEdSummary.Text = SafeGetVal(row, "Description", "")
            txtEdCoverUrl.Text = SafeGetVal(row, "CoverPath", "")
            txtEdBackUrl.Text = SafeGetVal(row, "BackPath", "")

            LoadImageSafe(picBookCover, txtEdCoverUrl.Text)
            LoadImageSafe(picBookBack, txtEdBackUrl.Text)
        Else
            lblSelectedId.Text = "Selection: None"
            picBookCover.Image = Nothing
            picBookBack.Image = Nothing
        End If
    End Sub

    ' ======================================================================
    ' Inline Editor: Save Changes to selected book
    ' ======================================================================
    Private Sub InlineSave(sender As Object, e As EventArgs)
        If isLocked Then
            MessageBox.Show("Please unlock editing first.", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Dim idStr As String = txtEdISBN.Text.Trim()
        If String.IsNullOrEmpty(idStr) Then
            ' No ID means this is a new book — call insert
            InlineInsert()
            Return
        End If
        Dim id As Integer
        If Not Integer.TryParse(idStr, id) Then
            MessageBox.Show("Cannot save: invalid record ID.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        If String.IsNullOrWhiteSpace(txtEdTitle.Text) Then
            MessageBox.Show("Title is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmd As New OleDbCommand(
                    "UPDATE Books SET [Title]=?, [Author]=?, [PubYear]=?, [Pages]=?, [Description]=?, [CoverPath]=?, [BackPath]=?, [EditedBy]=? WHERE [ID]=?", conn)
                cmd.Parameters.AddWithValue("?", txtEdTitle.Text)
                cmd.Parameters.AddWithValue("?", txtEdAuthor.Text)
                cmd.Parameters.AddWithValue("?", If(String.IsNullOrWhiteSpace(txtEdYear.Text), DBNull.Value, CObj(Val(txtEdYear.Text))))
                cmd.Parameters.AddWithValue("?", If(String.IsNullOrWhiteSpace(txtEdPages.Text), DBNull.Value, CObj(Val(txtEdPages.Text))))
                cmd.Parameters.AddWithValue("?", txtEdSummary.Text)
                cmd.Parameters.AddWithValue("?", txtEdCoverUrl.Text)
                cmd.Parameters.AddWithValue("?", txtEdBackUrl.Text)
                cmd.Parameters.AddWithValue("?", SessionUser)
                cmd.Parameters.AddWithValue("?", id)
                cmd.ExecuteNonQuery()
            End Using
            LoadBooks()
            MessageBox.Show("Book saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Save error: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ======================================================================
    ' Inline Editor: Clear fields for a new book entry
    ' ======================================================================
    Private Sub ClearEditorFields()
        txtEdISBN.Text = "" ' empty = new book mode
        txtEdTitle.Text = ""
        txtEdAuthor.Text = ""
        txtEdYear.Text = ""
        txtEdPages.Text = ""
        txtEdCategory.Text = ""
        txtEdSummary.Text = ""
        txtEdCoverUrl.Text = ""
        txtEdBackUrl.Text = ""
        picBookCover.Image = Nothing
        picBookBack.Image = Nothing
        lblSelectedId.Text = "Mode: New Book"
        dgvBooks.ClearSelection()
        txtEdTitle.Focus()
    End Sub

    Private Sub InlineNew(sender As Object, e As EventArgs)
        If isLocked Then
            MessageBox.Show("Please unlock editing first.", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ClearEditorFields()
    End Sub

    ' Helper: INSERT from inline editor fields
    Private Sub InlineInsert()
        If String.IsNullOrWhiteSpace(txtEdTitle.Text) Then
            MessageBox.Show("Title is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmd As New OleDbCommand(
                    "INSERT INTO Books ([Title],[Author],[PubYear],[Pages],[Description],[CoverPath],[BackPath],[LibraryName],[AddedBy]) VALUES (?,?,?,?,?,?,?,?,?)", conn)
                cmd.Parameters.AddWithValue("?", txtEdTitle.Text)
                cmd.Parameters.AddWithValue("?", txtEdAuthor.Text)
                cmd.Parameters.AddWithValue("?", If(String.IsNullOrWhiteSpace(txtEdYear.Text), DBNull.Value, CObj(Val(txtEdYear.Text))))
                cmd.Parameters.AddWithValue("?", If(String.IsNullOrWhiteSpace(txtEdPages.Text), DBNull.Value, CObj(Val(txtEdPages.Text))))
                cmd.Parameters.AddWithValue("?", txtEdSummary.Text)
                cmd.Parameters.AddWithValue("?", txtEdCoverUrl.Text)
                cmd.Parameters.AddWithValue("?", txtEdBackUrl.Text)
                cmd.Parameters.AddWithValue("?", currentLibrary)
                cmd.Parameters.AddWithValue("?", SessionUser)
                cmd.ExecuteNonQuery()
            End Using
            LoadBooks()
            ClearEditorFields() ' Clear fields
            MessageBox.Show("New book added successfully.", "Added", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Insert error: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ======================================================================
    ' Inline Editor: Delete currently selected book
    ' ======================================================================
    Private Sub InlineDelete(sender As Object, e As EventArgs)
        If isLocked Then
            MessageBox.Show("Please unlock editing first.", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Dim idStr As String = txtEdISBN.Text.Trim()
        Dim id As Integer
        If Not Integer.TryParse(idStr, id) Then
            MessageBox.Show("Please select a book from the grid first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim title As String = txtEdTitle.Text
        If MessageBox.Show("Delete '" & title & "'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            Try
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("DELETE FROM Books WHERE [ID] = ?", conn)
                    cmd.Parameters.AddWithValue("?", id)
                    cmd.ExecuteNonQuery()
                End Using
                LoadBooks()
                ClearEditorFields()
            Catch ex As Exception
                MessageBox.Show("Delete error: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    ' Robust Image Loading (Handles URLs or Local Paths)
    Private Sub LoadImageSafe(picBox As PictureBox, path As String)
        Try
            picBox.Image = Nothing
            If String.IsNullOrWhiteSpace(path) Then Return

            If path.ToLower().StartsWith("http") Then
                picBox.LoadAsync(path) ' Async for better UI response
            ElseIf File.Exists(path) Then
                ' Load via stream to prevent file locks
                Using fs As New FileStream(path, FileMode.Open, FileAccess.Read)
                    picBox.Image = Image.FromStream(fs)
                End Using
            End If
        Catch
            ' Fail silently with empty picbox
        End Try
    End Sub

    ' ======================================================================
    ' KL-700044 — Gemini AI Book Lookup (V3.6.0)
    ' Calls the Gemini 2.0 Flash API with the scanned ISBN, parses the
    ' returned JSON, and opens the Add Book form pre-filled.
    ' API Key is stored in Settings table under key "GeminiApiKey".
    ' ======================================================================
    Private Sub GeminiIsbnLookup(sender As Object, e As EventArgs)
        Dim isbn As String = txtScannerInput.Text.Trim()
        If String.IsNullOrEmpty(isbn) Then
            MessageBox.Show("Please enter an ISBN in the scanner field first.", "No ISBN", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' --- Retrieve or prompt for API key ---
        Dim apiKey As String = ""
        Try
            Using conn As New OleDbConnection(connString)
                conn.Open()
                Dim cmdGet As New OleDbCommand("SELECT SettingValue FROM Settings WHERE KeyName = 'GeminiApiKey'", conn)
                Dim res = cmdGet.ExecuteScalar()
                If res IsNot Nothing AndAlso res IsNot DBNull.Value Then apiKey = res.ToString()
            End Using
        Catch
        End Try

        If String.IsNullOrEmpty(apiKey) Then
            Dim keyPrompt As String = "Enter your Gemini API Key (from aistudio.google.com)." & vbCrLf & "It will be saved for future lookups:"
            apiKey = Interaction.InputBox(keyPrompt, "Gemini API Key Required", "")
            If String.IsNullOrEmpty(apiKey) Then Return
            ' Save it
            Try
                Using conn As New OleDbConnection(connString)
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

        ' --- Build Gemini prompt ---
        Dim prompt As String =
            "You are a library cataloging assistant. Given the ISBN: " & isbn &
            ", return ONLY a valid JSON object with these exact keys (no extra text, no markdown): " &
            "title, author, publisher, year, language, pages, subjects, description, isbn. " &
            "If a field is unknown leave it as an empty string. JSON only, nothing else."

        ' Safe JSON construction (V3.6.2)
        Dim q As String = Chr(34)
        Dim promptEscaped As String = prompt.Replace(q, "'")
        Dim jsonBody As String = "{" & q & "contents" & q & ":[{" & q & "parts" & q & ":[{" & q & "text" & q & ":" & q & promptEscaped & q & "}]}]}"

        Dim endpoint As String =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=" & apiKey

        ' --- Call API ---
        Dim responseText As String = ""
        Try
            Dim req As HttpWebRequest = DirectCast(WebRequest.Create(endpoint), HttpWebRequest)
            req.Method = "POST"
            req.ContentType = "application/json"
            req.Timeout = 20000
            Dim bodyBytes As Byte() = System.Text.Encoding.UTF8.GetBytes(jsonBody)
            req.ContentLength = bodyBytes.Length
            Using stream = req.GetRequestStream()
                stream.Write(bodyBytes, 0, bodyBytes.Length)
            End Using
            Using resp As HttpWebResponse = DirectCast(req.GetResponse(), HttpWebResponse)
                Using reader As New System.IO.StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8)
                    responseText = reader.ReadToEnd()
                End Using
            End Using
        Catch ex As Exception
            Dim msg As String = "Gemini API error: " & ex.Message
            If ex.Message.Contains("(429)") Then
                msg = "AI Rate Limit Reached (Error 429). " & vbCrLf & "Please wait a minute before trying again. The free tier has a limit on requests per minute."
            End If
            MessageBox.Show(msg, "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        ' --- Extract the inner JSON from Gemini response ---
        Dim innerJson As String = ""
        Dim textMatch = Regex.Match(responseText, """text""\s*:\s*""((?:[^""]|\\"")*)""", RegexOptions.Singleline)
        If textMatch.Success Then
            innerJson = textMatch.Groups(1).Value
            ' Clean up JSON: unescape specific sequences
            innerJson = innerJson.Replace("\n", " ")
            innerJson = innerJson.Replace(Chr(92) & Chr(34), Chr(34))
            innerJson = innerJson.Replace(Chr(92) & "/", "/")
        End If

        If String.IsNullOrEmpty(innerJson) Then
            MessageBox.Show("Gemini could not find data for this ISBN. Try a different source.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' --- Parse fields from extracted JSON ---
        Dim fTitle      As String = GeminiExtractField(innerJson, "title")
        Dim fAuthor     As String = GeminiExtractField(innerJson, "author")
        Dim fPublisher  As String = GeminiExtractField(innerJson, "publisher")
        Dim fYear       As String = GeminiExtractField(innerJson, "year")
        Dim fLanguage   As String = GeminiExtractField(innerJson, "language")
        Dim fPages      As String = GeminiExtractField(innerJson, "pages")
        Dim fSubjects   As String = GeminiExtractField(innerJson, "subjects")
        Dim fDesc       As String = GeminiExtractField(innerJson, "description")
        Dim fIsbn       As String = GeminiExtractField(innerJson, "isbn")
        If String.IsNullOrEmpty(fIsbn) Then fIsbn = isbn

        ' --- Populate inline editor with AI results ---
        ' Switch to the library view if not already there
        If cmbNavigation.SelectedItem IsNot Nothing AndAlso cmbNavigation.SelectedItem.ToString() <> "View Current Books" Then
            cmbNavigation.SelectedItem = "View Current Books"
        End If

        ' Clear and fill the inline editor
        ClearEditorFields()
        txtEdTitle.Text   = fTitle
        txtEdAuthor.Text  = fAuthor
        txtEdYear.Text    = fYear
        txtEdPages.Text   = fPages
        txtEdSummary.Text = fDesc
        lblSelectedId.Text = "Mode: New Book (AI pre-filled from ISBN " & fIsbn & ")"

        MessageBox.Show(
            "AI data pre-filled in the Book Editor panel." & vbCrLf &
            "Review the fields, then click '💾 Save Changes' to add the book.",
            "AI Lookup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ' Helper: Extracts a string value from a flat JSON object by key name
    Private Function GeminiExtractField(json As String, key As String) As String
        Try
            ' Regex for "key": "value"
            Dim m = Regex.Match(json, """" & key & """\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase)
            If m.Success Then Return m.Groups(1).Value.Trim()
            ' Regex for "key": value (numeric)
            Dim m2 = Regex.Match(json, """" & key & """\s*:\s*([0-9]+)", RegexOptions.IgnoreCase)
            If m2.Success Then Return m2.Groups(1).Value.Trim()
        Catch
        End Try
        Return ""
    End Function

    Private Sub OpenAddDialog(sender As Object, e As EventArgs)
        ' Legacy entry point: pre-fill ISBN from scanner and switch to inline editor
        cmbNavigation.SelectedItem = "View Current Books"
        ClearEditorFields()
        Dim candidateIsbn As String = txtScannerInput.Text.Trim()
        If Not String.IsNullOrEmpty(candidateIsbn) Then
            txtEdCategory.Text = candidateIsbn ' Show ISBN hint in category for now
        End If
        lblSelectedId.Text = "Mode: New Book"
        txtEdTitle.Focus()
    End Sub

    Private Sub OpenEditDialog(sender As Object, e As EventArgs)
        ' Legacy entry point: just focus the inline editor (already populated on row click)
        If dgvBooks.SelectedRows.Count = 0 Then
            MessageBox.Show("Please select a book from the grid.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        pnlEditor.Focus()
        txtEdTitle.Focus()
    End Sub

    ' Column Order Manager (V3.1.0)
    Private Sub OpenColumnOrderDialog(sender As Object, e As EventArgs)
        Using dlg As New ColumnOrderDialog(dgvBooks)
            If dlg.ShowDialog() = DialogResult.OK Then
                SaveGridLayout()
            End If
        End Using
    End Sub

    Private Sub SaveGridLayout()
        Try
            Dim orderData As New List(Of String)()
            For Each col As DataGridViewColumn In dgvBooks.Columns
                orderData.Add(col.Name & ":" & col.DisplayIndex & ":" & col.Width & ":" & col.Visible)
            Next
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grid_layout.txt"), String.Join("|", orderData))
            ' Save window size
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_size.txt"), Me.Width & "," & Me.Height)
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
            Dim sizeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_size.txt")
            If File.Exists(sizeFile) Then
                Dim sz = File.ReadAllText(sizeFile).Split(","c)
                Me.Size = New Size(CInt(sz(0)), CInt(sz(1)))
            End If
        Catch
        End Try
    End Sub

    ' Multi-Database Connection Logic (V3.0.0)
    Private Sub OpenDbSettingsDialog(sender As Object, e As EventArgs)
        Using dlg As New DatabaseSettingsDialog(connString)
            If dlg.ShowDialog() = DialogResult.OK Then
                connString = dlg.NewConnString
                File.WriteAllText(configPath, connString)
                MessageBox.Show("Connection updated. The application will now re-initialize.", "Database Updated")
                InitializeDatabase()
                LoadLibraries()
                SwitchView("Welcome Screen")
            End If
        End Using
    End Sub

    ' Robustly gets value from a grid cell by name with fallback
    Private Function SafeGetVal(row As DataGridViewRow, colName As String, Optional fallback As String = "") As String
        Try
            If row.DataGridView.Columns.Contains(colName) Then
                Dim val = row.Cells(colName).Value
                If val Is Nothing OrElse val Is DBNull.Value Then Return fallback
                Return val.ToString()
            End If
        Catch
        End Try
        Return fallback
    End Function

    ' Safely strips active row payload
    Private Function GetSelectedID() As Integer
        If dgvBooks.SelectedRows.Count > 0 Then
            Return Convert.ToInt32(dgvBooks.SelectedRows(0).Cells("ID").Value)
        End If
        Return -1
    End Function

    ' Applies Registry fix to ensure WebBrowser control uses modern standards (IE11)
    Private Sub SetBrowserEmulation()
        Try
            Dim appName As String = "LibraryAppVB.exe"
            Dim keyPath As String = "Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"
            Using key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, True)
                If key IsNot Nothing Then
                    key.SetValue(appName, 11001, Microsoft.Win32.RegistryValueKind.DWord)
                End If
            End Using
        Catch
            ' Best effort, might fail due to permissions
        End Try
    End Sub

    ' SQL DELETE request explicitly scoped to selected Primary Key
    Private Sub DeleteBook(sender As Object, e As EventArgs)
        Dim id As Integer = GetSelectedID()
        If id = -1 Then Return
        If MessageBox.Show("Delete this book?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            Try
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("DELETE FROM Books WHERE ID = ?", conn)
                    cmd.Parameters.AddWithValue("?", id)
                    cmd.ExecuteNonQuery()
                End Using
                LoadBooks()
            Catch ex As Exception
            End Try
        End If
    End Sub

    ' Launches default browser to search for Book Metadata
    Private Sub OpenSearchOnline(sender As Object, e As EventArgs)
        If dgvBooks.SelectedRows.Count = 0 Then
            MessageBox.Show("Please select a book first.", "Search Online", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim row As DataGridViewRow = dgvBooks.SelectedRows(0)
        Dim title As String = If(row.Cells("Title").Value Is DBNull.Value, "", row.Cells("Title").Value.ToString())
        Dim isbn As String = If(row.Cells("ISBN").Value Is DBNull.Value, "", row.Cells("ISBN").Value.ToString())
        Dim author As String = If(row.Cells("Author").Value Is DBNull.Value, "", row.Cells("Author").Value.ToString())

        Dim query As String = ""
        If Not String.IsNullOrWhiteSpace(isbn) Then
            query = isbn
        Else
            query = title & " " & author
        End If

        If Not String.IsNullOrWhiteSpace(query) Then
            Dim url As String = "https://www.google.com/search?q=book+info+" & System.Uri.EscapeDataString(query)
            Try
                Diagnostics.Process.Start(url)
            Catch ex As Exception
                MessageBox.Show("Could not launch browser: " & ex.Message)
            End Try
        End If
    End Sub

    ' Scrapes Google Books or ISBNSearch.org for book info based on text in txtScannerInput
    Private Sub SearchByIsbn(sender As Object, e As EventArgs)
        Dim isbn As String = txtScannerInput.Text.Trim()
        If String.IsNullOrEmpty(isbn) Then Return

        Me.Cursor = Cursors.WaitCursor
        Try
            Dim title As String = "", author As String = "", publisher As String = "", year As String = ""
            Dim found As Boolean = False

            ' --- STEP 1: Try Google Books API (High reliability, JSON) ---
            Try
                Dim gUrl As String = "https://www.googleapis.com/books/v1/volumes?q=isbn:" & isbn
                Dim gReq As System.Net.HttpWebRequest = System.Net.HttpWebRequest.Create(gUrl)
                Using gResp As System.Net.HttpWebResponse = gReq.GetResponse()
                    Using sr As New StreamReader(gResp.GetResponseStream())
                        Dim json As String = sr.ReadToEnd()
                        If json.Contains("""totalItems"": 1") Or json.Contains("""totalItems"": 2") Then
                            title = System.Text.RegularExpressions.Regex.Match(json, """title"":\s*""(.*?)""").Groups(1).Value
                            author = System.Text.RegularExpressions.Regex.Match(json, """authors"":\s*\[\s*""(.*?)""").Groups(1).Value
                            publisher = System.Text.RegularExpressions.Regex.Match(json, """publisher"":\s*""(.*?)""").Groups(1).Value
                            year = System.Text.RegularExpressions.Regex.Match(json, """publishedDate"":\s*""(\d{4})").Groups(1).Value
                            found = True
                        End If
                    End Using
                End Using
            Catch
            End Try

            ' --- STEP 2: Fallback to ISBNSearch.org (Web Scraping) ---
            If Not found Then
                Try
                    Dim url As String = "https://isbnsearch.org/isbn/" & isbn
                    Dim html As String = ""
                    Dim req As System.Net.HttpWebRequest = System.Net.HttpWebRequest.Create(url)
                    req.UserAgent = "Mozilla/5.0"
                    Using resp As System.Net.HttpWebResponse = req.GetResponse()
                        Using sr As New StreamReader(resp.GetResponseStream())
                            html = sr.ReadToEnd()
                        End Using
                    End Using
                    title = System.Text.RegularExpressions.Regex.Match(html, "<h2>(.*?)</h2>").Groups(1).Value
                    author = System.Text.RegularExpressions.Regex.Match(html, "<b>Author:</b> (.*?)</p>").Groups(1).Value
                    publisher = System.Text.RegularExpressions.Regex.Match(html, "<b>Publisher:</b> (.*?)</p>").Groups(1).Value
                    Dim publishedStr As String = System.Text.RegularExpressions.Regex.Match(html, "<b>Published:</b> (.*?)</p>").Groups(1).Value
                    year = System.Text.RegularExpressions.Regex.Match(publishedStr, "(\d{4})").Groups(1).Value
                    If Not String.IsNullOrEmpty(title) Then found = True
                Catch
                End Try
            End If

            If Not found Then
                Dim res As DialogResult = MessageBox.Show("ISBN " & isbn & " was not found in our site database." & vbCrLf & vbCrLf & "Would you like to search on Google?", "Not found", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If res = DialogResult.Yes Then
                    Dim gUrl As String = "https://www.google.com/search?q=isbn+" & isbn
                    Try
                        Diagnostics.Process.Start("msedge.exe", "--app=""" & gUrl & """ --window-size=800,600")
                    Catch
                        Diagnostics.Process.Start(gUrl)
                    End Try
                End If
                Return
            End If

            ' Found: populate inline editor with scanned data
            cmbNavigation.SelectedItem = "View Current Books"
            ClearEditorFields()
            txtEdTitle.Text  = title
            txtEdAuthor.Text = author
            txtEdYear.Text   = year
            lblSelectedId.Text = "Mode: New Book (ISBN Scanned: " & isbn & ")"
            txtScannerInput.Clear()
            MessageBox.Show("Book data pre-filled in the Editor panel." & vbCrLf & "Review and click '💾 Save Changes' to add.", "ISBN Lookup", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            MessageBox.Show("Unexpected error during lookup: " & ex.Message, "ISBN Search", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Me.Cursor = Cursors.Default
        End Try
    End Sub

    ' Migrates all web image URLs to local files
    Private Sub MigrateWebImagesToLocal(sender As Object, e As EventArgs)
        If MessageBox.Show("This will attempt to download all web-based cover and back image URLs to local files and update the database. This might take a while. Continue?", "Confirm Migration", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            Me.Cursor = Cursors.WaitCursor
            Dim updatedCount As Integer = 0
            Try
                Using conn As New OleDbConnection(connString)
                    conn.Open()
                    Dim cmd As New OleDbCommand("SELECT ID, ISBN, CoverPath, BackPath FROM Books", conn)
                    Dim reader As OleDbDataReader = cmd.ExecuteReader()
                    Dim booksToUpdate As New List(Of Tuple(Of Integer, String, String, String))

                    While reader.Read()
                        Dim id As Integer = reader.GetInt32(0)
                        Dim isbn As String = If(reader("ISBN") Is DBNull.Value, "", reader("ISBN").ToString())
                        Dim coverPath As String = If(reader("CoverPath") Is DBNull.Value, "", reader("CoverPath").ToString())
                        Dim backPath As String = If(reader("BackPath") Is DBNull.Value, "", reader("BackPath").ToString())
                        booksToUpdate.Add(New Tuple(Of Integer, String, String, String)(id, isbn, coverPath, backPath))
                    End While
                    reader.Close()

                    For Each book In booksToUpdate
                        Dim id As Integer = book.Item1
                        Dim isbn As String = book.Item2
                        Dim coverPath As String = book.Item3
                        Dim backPath As String = book.Item4

                        Dim newCoverPath As String = coverPath
                        If Not String.IsNullOrWhiteSpace(coverPath) AndAlso coverPath.ToLower().StartsWith("http") Then
                            newCoverPath = EnsureLocalImage(coverPath, isbn, "cover")
                        End If

                        Dim newBackPath As String = backPath
                        If Not String.IsNullOrWhiteSpace(backPath) AndAlso backPath.ToLower().StartsWith("http") Then
                            newBackPath = EnsureLocalImage(backPath, isbn, "back")
                        End If

                        If newCoverPath <> coverPath OrElse newBackPath <> backPath Then
                            Dim updateCmd As New OleDbCommand("UPDATE Books SET CoverPath = ?, BackPath = ? WHERE ID = ?", conn)
                            updateCmd.Parameters.AddWithValue("?", newCoverPath)
                            updateCmd.Parameters.AddWithValue("?", newBackPath)
                            updateCmd.Parameters.AddWithValue("?", id)
                            updateCmd.ExecuteNonQuery()
                            updatedCount += 1
                        End If
                    Next
                End Using
                MessageBox.Show(String.Format("Migration complete. {0} books updated.", updatedCount), "Migration Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                LoadBooks()
            Catch ex As Exception
                MessageBox.Show("Image migration failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.Cursor = Cursors.Default
            End Try
        End If
    End Sub

    ' Shared function to download and save an image locally, returning the local path
    Public Shared Function EnsureLocalImage(imageUrl As String, isbn As String, imageType As String) As String
        If String.IsNullOrWhiteSpace(imageUrl) OrElse Not imageUrl.ToLower().StartsWith("http") Then
            Return imageUrl ' Not a web URL, return as is
        End If

        Try
            Dim imageDir As String = Path.Combine(Application.StartupPath, "BookImages")
            If Not Directory.Exists(imageDir) Then
                Directory.CreateDirectory(imageDir)
            End If

            Dim fileName As String = ""
            If Not String.IsNullOrWhiteSpace(isbn) Then
                fileName = String.Format("{0}_{1}{2}", isbn, imageType, Path.GetExtension(imageUrl))
            Else
                fileName = String.Format("{0}_{1}{2}", Guid.NewGuid().ToString(), imageType, Path.GetExtension(imageUrl))
            End If
            
            Dim localPath As String = Path.Combine(imageDir, fileName)

            Using webClient As New System.Net.WebClient()
                webClient.DownloadFile(imageUrl, localPath)
            End Using

            Return localPath
        Catch ex As Exception
            ' Log or handle error, return original URL if download fails
            Console.WriteLine(String.Format("Error downloading image {0}: {1}", imageUrl, ex.Message))
            Return imageUrl
        End Try
    End Function

End Class


' ======================================================================
' CLASS: IsbnPreviewForm
' Description: A form to review scraped ISBN information.
' ======================================================================
Public Class IsbnPreviewForm
    Inherits Form

    Public Sub New(title As String, author As String, isbn As String, publisher As String, year As String)
        Me.Text = "ISBN Search Result Preview"
        Me.Size = New Size(500, 400)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.BackColor = Color.White
        Me.Font = New Font("Segoe UI", 10)

        Dim lblHead As New Label() With {.Text = "Results from ISBNSearch.org:", .Location = New Point(20, 20), .AutoSize = True, .Font = New Font("Segoe UI", 12, FontStyle.Bold)}
        Me.Controls.Add(lblHead)

        Dim y As Integer = 70
        AddField("Title:", title, y) : y += 40
        AddField("Author:", author, y) : y += 40
        AddField("ISBN:", isbn, y) : y += 40
        AddField("Publisher:", publisher, y) : y += 40
        AddField("Year:", year, y) : y += 60

        Dim btnApply As New Button() With {.Text = "Apply to New Book", .DialogResult = DialogResult.OK, .Location = New Point(100, y), .Size = New Size(150, 40), .BackColor = Color.LightGreen, .FlatStyle = FlatStyle.Flat}
        Dim btnCancel As New Button() With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Location = New Point(260, y), .Size = New Size(100, 40), .BackColor = Color.LightGray, .FlatStyle = FlatStyle.Flat}
        
        Me.Controls.Add(btnApply)
        Me.Controls.Add(btnCancel)
        Me.AcceptButton = btnApply
        Me.CancelButton = btnCancel
    End Sub

    Private Sub AddField(lblText As String, valText As String, y As Integer)
        Dim lbl As New Label() With {.Text = lblText, .Location = New Point(20, y), .AutoSize = True, .ForeColor = Color.DimGray}
        Dim val As New Label() With {.Text = If(String.IsNullOrEmpty(valText), "Not found", valText), .Location = New Point(120, y), .AutoSize = True, .Font = New Font("Segoe UI", 10, FontStyle.Bold)}
        Me.Controls.Add(lbl)
        Me.Controls.Add(val)
    End Sub
End Class

' ======================================================================
' CLASS: MetadataSelectForm (V22 - Scrolling List)
' ======================================================================
Public Class MetadataSelectForm
    Inherits Form

    Private flp As FlowLayoutPanel
    Public SelectedFields As New Dictionary(Of String, String)()
    Private checkBoxes As New List(Of CheckBox)()

    Public Sub New(props As Dictionary(Of String, String))
        Me.Text = "Metadata Harvest Review (V22)"
        Me.Size = New Size(700, 600)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.BackColor = Color.White

        flp = New FlowLayoutPanel()
        flp.Dock = DockStyle.Top
        flp.Height = 500
        flp.AutoScroll = True
        flp.FlowDirection = FlowDirection.TopDown
        flp.WrapContents = False
        Me.Controls.Add(flp)

        For Each kvp In props
            If Not String.IsNullOrEmpty(kvp.Value) Then
                Dim chk As New CheckBox() With {
                    .Text = String.Format("{0}: {1}", kvp.Key, If(kvp.Value.Length > 80, kvp.Value.Substring(0, 77) & "...", kvp.Value)),
                    .Tag = kvp,
                    .Checked = True,
                    .Width = 650,
                    .AutoSize = False,
                    .Height = 30
                }
                flp.Controls.Add(chk)
                checkBoxes.Add(chk)
            End If
        Next

        Dim pnlBtns As New Panel() With {.Dock = DockStyle.Bottom, .Height = 60}
        Dim btnOK As New Button() With {.Text = "Apply Selection", .DialogResult = DialogResult.OK, .Location = New Point(250, 10), .Size = New Size(200, 40), .BackColor = Color.LightGreen, .FlatStyle = FlatStyle.Flat}
        AddHandler btnOK.Click, Sub()
            For Each chk In checkBoxes
                If chk.Checked Then
                    Dim kvp = DirectCast(chk.Tag, KeyValuePair(Of String, String))
                    SelectedFields(kvp.Key) = kvp.Value
                End If
            Next
        End Sub
        pnlBtns.Controls.Add(btnOK)
        Me.Controls.Add(pnlBtns)
    End Sub
End Class

' ======================================================================
' NOTE: GoogleSearchForm class removed in favor of modern Edge App Mode launch.
' ======================================================================

' ======================================================================
' CLASS: ColumnOrderDialog
' Description: UI for reordering and resizing grid columns
' ======================================================================
Public Class ColumnOrderDialog
    Inherits Form
    Private _grid As DataGridView
    Private lstCols As ListBox
    Private btnUp, btnDown, btnOK As Button

    Public Sub New(grid As DataGridView)
        _grid = grid
        Me.Text = "Column Settings"
        Me.Size = New Size(400, 500)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.StartPosition = FormStartPosition.CenterParent

        lstCols = New ListBox() With {.Location = New Point(20, 20), .Size = New Size(250, 350)}
        For Each col As DataGridViewColumn In _grid.Columns.Cast(Of DataGridViewColumn)().OrderBy(Function(c) c.DisplayIndex)
            lstCols.Items.Add(col.Name)
        Next
        Me.Controls.Add(lstCols)

        btnUp = New Button() With {.Text = "▲", .Location = New Point(280, 50), .Size = New Size(40, 40)}
        btnDown = New Button() With {.Text = "▼", .Location = New Point(280, 100), .Size = New Size(40, 40)}
        AddHandler btnUp.Click, AddressOf MoveUp
        AddHandler btnDown.Click, AddressOf MoveDown
        Me.Controls.Add(btnUp) : Me.Controls.Add(btnDown)

        btnOK = New Button() With {.Text = "Apply Order", .Location = New Point(100, 400), .Size = New Size(120, 40), .BackColor = Color.LightGreen, .DialogResult = DialogResult.OK}
        AddHandler btnOK.Click, AddressOf SaveSettings
        Me.Controls.Add(btnOK)
    End Sub

    Private Sub MoveUp()
        Dim idx = lstCols.SelectedIndex
        If idx > 0 Then
            Dim item = lstCols.Items(idx)
            lstCols.Items.RemoveAt(idx)
            lstCols.Items.Insert(idx - 1, item)
            lstCols.SelectedIndex = idx - 1
        End If
    End Sub

    Private Sub MoveDown()
        Dim idx = lstCols.SelectedIndex
        If idx >= 0 And idx < lstCols.Items.Count - 1 Then
            Dim item = lstCols.Items(idx)
            lstCols.Items.RemoveAt(idx)
            lstCols.Items.Insert(idx + 1, item)
            lstCols.SelectedIndex = idx + 1
        End If
    End Sub

    Private Sub SaveSettings()
        For i As Integer = 0 To lstCols.Items.Count - 1
            Dim colName = lstCols.Items(i).ToString()
            _grid.Columns(colName).DisplayIndex = i
        Next
    End Sub
End Class

' ======================================================================
' CLASS: DatabaseSettingsDialog
' Allows switching between Local Access and SQL Server
' ======================================================================
Public Class DatabaseSettingsDialog
    Inherits Form
    Public NewConnString As String
    Private txtConn As TextBox
    Private btnTest, btnSave, btnBrowse As Button
    Private rbAccess, rbSql As RadioButton

    Public Sub New(currentConn As String)
        Me.Text = "Database Connection Manager"
        Me.Size = New Size(600, 450)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = Color.White
        
        Dim y As Integer = 20
        Dim lblInfo As New Label() With {.Text = "Choose your database provider:", .Location = New Point(20, y), .AutoSize = True, .Font = New Font("Segoe UI", 10, FontStyle.Bold)}
        Me.Controls.Add(lblInfo) : y += 40

        rbAccess = New RadioButton() With {.Text = "Local MS Access (.mdb)", .Location = New Point(30, y), .Width = 200, .Checked = currentConn.Contains("Microsoft.Jet")}
        Me.Controls.Add(rbAccess) : y += 30
        
        rbSql = New RadioButton() With {.Text = "Online SQL Server (OLEDB)", .Location = New Point(30, y), .Width = 200, .Checked = currentConn.Contains("SQLOLEDB") Or currentConn.Contains("MSOLEDBSQL")}
        Me.Controls.Add(rbSql) : y += 40

        Dim lblConn As New Label() With {.Text = "Connection String:", .Location = New Point(20, y), .AutoSize = True}
        Me.Controls.Add(lblConn) : y += 25
        
        txtConn = New TextBox() With {.Text = currentConn, .Location = New Point(20, y), .Size = New Size(540, 80), .Multiline = True}
        Me.Controls.Add(txtConn) : y += 95

        btnBrowse = New Button() With {.Text = "📁 Browse Access File", .Location = New Point(20, y), .Size = New Size(150, 35), .BackColor = Color.LightGray}
        AddHandler btnBrowse.Click, AddressOf BrowseAccess
        Me.Controls.Add(btnBrowse)

        btnTest = New Button() With {.Text = "⚡ Test Connection", .Location = New Point(180, y), .Size = New Size(150, 35), .BackColor = Color.LightCyan}
        AddHandler btnTest.Click, AddressOf TestConn
        Me.Controls.Add(btnTest)
        
        y += 60
        btnSave = New Button() With {.Text = "Apply & Save", .Location = New Point(150, y), .Size = New Size(120, 40), .BackColor = Color.LightGreen, .DialogResult = DialogResult.OK}
        AddHandler btnSave.Click, Sub() NewConnString = txtConn.Text
        Me.Controls.Add(btnSave)

        Dim btnCancel As New Button() With {.Text = "Cancel", .Location = New Point(280, y), .Size = New Size(120, 40), .BackColor = Color.WhiteSmoke, .DialogResult = DialogResult.Cancel}
        Me.Controls.Add(btnCancel)
    End Sub

    Private Sub BrowseAccess(sender As Object, e As EventArgs)
        Using ofd As New OpenFileDialog()
            ofd.Filter = "Access Database|*.mdb"
            If ofd.ShowDialog() = DialogResult.OK Then
                txtConn.Text = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" & ofd.FileName & ";Jet OLEDB:Database Password=dbpass;"
                rbAccess.Checked = True
            End If
        End Using
    End Sub

    Private Sub TestConn(sender As Object, e As EventArgs)
        Try
            Using conn As New OleDbConnection(txtConn.Text)
                conn.Open()
                MessageBox.Show("Connection Successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Using
        Catch ex As Exception
            MessageBox.Show("Connection Failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class

' ======================================================================
' Description: Modern gateway for library authentication.
' ======================================================================
Public Class LoginForm
    Inherits Form
    Public UserName As String = ""
    Public UserRole As String = ""
    Private txtUser, txtPass As TextBox

    Public Sub New()
        Me.Text = "My K Library"
        Me.Size = New Size(440, 360)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.BackColor = Color.White
        Me.Font = New Font("Segoe UI", 10)

        ' Modern header panel (Dark Navy)
        Dim pnlTop As New Panel() With {.Dock = DockStyle.Top, .Height = 90, .BackColor = Color.FromArgb(28, 40, 60)}
        Me.Controls.Add(pnlTop)

        If File.Exists("bookic1.png") Then
            Dim pic As New PictureBox() With {
                .Size = New Size(48, 48), .Location = New Point(16, 20),
                .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.Transparent}
            pic.Image = Image.FromFile("bookic1.png")
            pnlTop.Controls.Add(pic)
        End If

        Dim lblAppName As New Label() With {
            .Text = "My K Library",
            .Font = New Font("Segoe UI", 16, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = Color.Transparent,
            .Location = New Point(74, 18), .AutoSize = True}
        pnlTop.Controls.Add(lblAppName)

        Dim lblSub As New Label() With {
            .Text = "Secure Archive Access",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(200, 210, 220),
            .BackColor = Color.Transparent,
            .Location = New Point(75, 50), .AutoSize = True}
        pnlTop.Controls.Add(lblSub)

        ' Form fields
        Dim y As Integer = 110
        Dim lblU As New Label() With {.Text = "Username", .Location = New Point(40, y), .AutoSize = True, .ForeColor = Color.FromArgb(80, 90, 100)}
        Me.Controls.Add(lblU) : y += 22
        txtUser = New TextBox() With {.Location = New Point(40, y), .Width = 360, .Height = 32, .Font = New Font("Segoe UI", 11), .BorderStyle = BorderStyle.FixedSingle}
        Me.Controls.Add(txtUser) : y += 48

        Dim lblP As New Label() With {.Text = "Password", .Location = New Point(40, y), .AutoSize = True, .ForeColor = Color.FromArgb(80, 90, 100)}
        Me.Controls.Add(lblP) : y += 22
        txtPass = New TextBox() With {.Location = New Point(40, y), .Width = 360, .Height = 32, .Font = New Font("Segoe UI", 11), .BorderStyle = BorderStyle.FixedSingle, .UseSystemPasswordChar = True}
        Me.Controls.Add(txtPass) : y += 52

        Dim btnLogin As New Button() With {
            .Text = "Login", .Location = New Point(40, y), .Size = New Size(360, 42),
            .BackColor = Color.FromArgb(28, 40, 60), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .Cursor = Cursors.Hand}
        btnLogin.FlatAppearance.BorderSize = 0
        AddHandler btnLogin.Click, AddressOf TryLogin
        Me.Controls.Add(btnLogin)
        Me.AcceptButton = btnLogin
    End Sub

    Private Sub TryLogin(sender As Object, e As EventArgs)
        Try
            Dim tempConn = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=Library.mdb;Jet OLEDB:Database Password=dbpass;"
            Dim configPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt")
            If File.Exists(configPath) Then tempConn = File.ReadAllText(configPath).Trim()

            Using conn As New OleDbConnection(tempConn)
                conn.Open()
                Dim dtSchema As DataTable = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, New Object() {Nothing, Nothing, "Users", "TABLE"})
                If dtSchema.Rows.Count = 0 Then
                    Dim createSql As String = "CREATE TABLE [Users] ([UserID] COUNTER PRIMARY KEY, [Username] VARCHAR(50), [Password] VARCHAR(50), [Role] VARCHAR(20))"
                    Using cmdC As New OleDbCommand(createSql, conn) : cmdC.ExecuteNonQuery() : End Using
                    Dim seedSql As String = "INSERT INTO [Users] ([Username], [Password], [Role]) VALUES ('admin', '2026', 'SuperAdmin')"
                    Using cmdS As New OleDbCommand(seedSql, conn) : cmdS.ExecuteNonQuery() : End Using
                End If

                Dim cmd As New OleDbCommand("SELECT [Role] FROM [Users] WHERE [Username] = ? AND [Password] = ?", conn)
                cmd.Parameters.AddWithValue("?", txtUser.Text)
                cmd.Parameters.AddWithValue("?", txtPass.Text)
                Dim res = cmd.ExecuteScalar()
                If res IsNot Nothing Then
                    UserName = txtUser.Text
                    UserRole = res.ToString()
                    Me.DialogResult = DialogResult.OK
                Else
                    MessageBox.Show("Invalid credentials. Try admin / 2026", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show("Login System Error: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class

' ======================================================================
' CLASS: UserManagerDialog
' Description: SuperAdmin interface for user provisioning.
' ======================================================================
Public Class UserManagerDialog
    Inherits Form
    Private txtNewUser, txtNewPass As TextBox
    Private cmbRole As ComboBox

    Public Sub New()
        Me.Text = "User Management"
        Me.Size = New Size(400, 400)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = Color.WhiteSmoke

        Dim lblU As New Label() With {.Text = "Username:", .Location = New Point(20, 25), .AutoSize = True}
        txtNewUser = New TextBox() With {.Location = New Point(120, 20), .Size = New Size(200, 30)}
        Me.Controls.Add(lblU) : Me.Controls.Add(txtNewUser)
        
        Dim lblP As New Label() With {.Text = "Password:", .Location = New Point(20, 65), .AutoSize = True}
        txtNewPass = New TextBox() With {.Location = New Point(120, 60), .Size = New Size(200, 30)}
        Me.Controls.Add(lblP) : Me.Controls.Add(txtNewPass)

        Dim lblR As New Label() With {.Text = "Role:", .Location = New Point(20, 105), .AutoSize = True}
        cmbRole = New ComboBox() With {.Location = New Point(120, 100), .Size = New Size(200, 30), .DropDownStyle = ComboBoxStyle.DropDownList}
        cmbRole.Items.AddRange(New String() {"Administrator", "Editor", "Viewer"})
        cmbRole.SelectedIndex = 2
        Me.Controls.Add(lblR) : Me.Controls.Add(cmbRole)

        Dim btnAdd As New Button() With {.Text = "Create User", .Location = New Point(120, 150), .Size = New Size(200, 40), .BackColor = Color.LightBlue, .FlatStyle = FlatStyle.Flat}
        AddHandler btnAdd.Click, AddressOf CreateUser
        Me.Controls.Add(btnAdd)
    End Sub

    Private Sub CreateUser(sender As Object, e As EventArgs)
        Try
            Using conn As New OleDbConnection(MainForm.connString)
                conn.Open()
                Dim cmd As New OleDbCommand("INSERT INTO Users (Username, Password, Role) VALUES (?, ?, ?)", conn)
                cmd.Parameters.AddWithValue("?", txtNewUser.Text)
                cmd.Parameters.AddWithValue("?", txtNewPass.Text)
                cmd.Parameters.AddWithValue("?", cmbRole.SelectedItem.ToString())
                cmd.ExecuteNonQuery()
                MessageBox.Show("User '" & txtNewUser.Text & "' created successfully.")
            End Using
        Catch ex As Exception
            MessageBox.Show("Configuration Error: " & ex.Message)
        End Try
    End Sub
End Class

' ======================================================================
' MODULE: Program
' Description: The main entry point of the VB.NET WinForms application.
' It initializes visual styling and passes control to the MainForm.
' ======================================================================
Module Program
    Public SessionUser As String = ""
    Public SessionRole As String = ""

    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        
        Using login As New LoginForm()
            If login.ShowDialog() = DialogResult.OK Then
                SessionUser = login.UserName
                SessionRole = login.UserRole
                Application.Run(New MainForm())
            End If
        End Using
    End Sub
End Module
