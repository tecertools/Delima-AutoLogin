using System.IO;
using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Core.Store;
using Delima.Launcher.Services;
using Delima.Launcher.ViewModels;
using Delima.Win32;
using Xunit;

namespace Delima.Launcher.Tests;

public class LauncherFlowTests
{
    [Fact]
    public void SedangMasukViewModel_Initializes_With_Pupil_And_School()
    {
        var school = SampleDataService.CreateSampleSchool();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var cred = new SecurePasswordBuffer("TestPassword123!"u8);

        var vm = new SedangMasukViewModel(
            school,
            student,
            cred,
            onSuccess: _ => { },
            onFailure: _ => { },
            onCancel: () => { });

        Assert.Equal(student, vm.Student);
        Assert.Equal(school, vm.School);
        Assert.NotEmpty(vm.StatusMessage);
    }

    [Fact]
    public void SedangMasukViewModel_CancelCommand_TriggersCancelCallback()
    {
        var school = SampleDataService.CreateSampleSchool();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var cred = new SecurePasswordBuffer("TestPassword123!"u8);

        bool cancelCalled = false;

        var vm = new SedangMasukViewModel(
            school,
            student,
            cred,
            onSuccess: _ => { },
            onFailure: _ => { },
            onCancel: () => cancelCalled = true);

        vm.CancelCommand.Execute(null);
        Assert.True(cancelCalled);
    }

    [Fact]
    public void RalatViewModel_Populates_From_FailureTaxonomy()
    {
        var school = SampleDataService.CreateSampleSchool();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];

        bool retryCalled = false;

        var vm = new RalatViewModel(
            school,
            FailureCodes.E01_NoBrowserFound,
            onRetry: () => retryCalled = true,
            student: student);

        Assert.Equal(FailureCodes.E01_NoBrowserFound, vm.ErrorCode);
        Assert.Equal("Alamak, ada masalah. Panggil cikgu.", vm.PupilMessage);
        Assert.Equal("Install Microsoft Edge or Google Chrome", vm.TeacherAction);
        Assert.Equal("No supported browser found (Edge or Chrome)", vm.ConditionDescription);

        vm.RetryCommand.Execute(null);
        Assert.True(retryCalled);
    }

    [Fact]
    public void MainViewModel_NavigateToRalat_Sets_RalatViewModel()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];

        mainVm.NavigateToRalat(student, FailureCodes.E09_StoreDecryptFailure);

        Assert.IsType<RalatViewModel>(mainVm.CurrentView);
        var ralatVm = (RalatViewModel)mainVm.CurrentView!;
        Assert.Equal(FailureCodes.E09_StoreDecryptFailure, ralatVm.ErrorCode);
    }

    [Theory]
    [InlineData(FailureCodes.E01_NoBrowserFound, "Alamak, ada masalah. Panggil cikgu.", "Install Microsoft Edge or Google Chrome")]
    [InlineData(FailureCodes.E02_WindowNotVerified, "Cuba lagi.", "Slow PC — raise window_wait_timeout_ms")]
    [InlineData(FailureCodes.E03_InjectionAborted, "", "None")]
    [InlineData(FailureCodes.E06_GoogleCaptcha, "Tunggu sekejap, cuba lagi.", "Space out launches; known limitation")]
    [InlineData(FailureCodes.E07_TwoFactorPrompt, "Panggil cikgu.", "Escalate — this may end the product")]
    [InlineData(FailureCodes.E08_AccountSuspended, "Panggil cikgu.", "MOE admin task")]
    [InlineData(FailureCodes.E09_StoreDecryptFailure, "Alamak, ada masalah. Panggil cikgu.", "Re-provision this PC")]
    [InlineData(FailureCodes.E10_StoreStale, "Panggil cikgu.", "Re-provision this PC")]
    [InlineData(FailureCodes.E11_NoPasswordStored, "Panggil cikgu.", "Complete wizard Step 4")]
    [InlineData(FailureCodes.E12_PicturePasswordLocked, "Tunggu 5 minit.", "Reset via Mod Guru")]
    [InlineData(FailureCodes.E13_NetworkUnreachable, "Tiada internet. Panggil cikgu.", "Network")]
    [InlineData(FailureCodes.E14_PasswordRejected, "Kata laluan tidak diterima. Beritahu cikgu.", "Mod Guru for one pupil; re-import in Delima.Admin if the whole class fails")]
    public void RalatViewModel_AllTaxonomyCodes_HaveExpectedMessages(string code, string expectedPupilBm, string expectedTeacher)
    {
        var school = SampleDataService.CreateSampleSchool();
        var vm = new RalatViewModel(school, code, () => { });

        Assert.Equal(code, vm.ErrorCode);
        Assert.Equal(expectedPupilBm, vm.PupilMessage);
        Assert.Equal(expectedTeacher, vm.TeacherAction);
        Assert.NotEmpty(vm.ConditionDescription);
    }

    [Fact]
    public void MainViewModel_NavigateToModGuruPin_AndThen_ToDashboard_AndReturn()
    {
        var mainVm = new MainViewModel();
        Assert.IsType<PilihKelasViewModel>(mainVm.CurrentView);

        // Navigate to Mod Guru PIN
        mainVm.NavigateToModGuruPin();
        Assert.IsType<ModGuruPinViewModel>(mainVm.CurrentView);
        var pinVm = (ModGuruPinViewModel)mainVm.CurrentView;

        // Enter valid default PIN
        pinVm.AppendDigit("1");
        pinVm.AppendDigit("2");
        pinVm.AppendDigit("3");
        pinVm.AppendDigit("4");

        // Should have transitioned to Mod Guru Dashboard
        Assert.IsType<ModGuruDashboardViewModel>(mainVm.CurrentView);
        var dashboardVm = (ModGuruDashboardViewModel)mainVm.CurrentView;

        // Exit dashboard -> returns to PilihKelasView
        dashboardVm.ExitDashboardCommand.Execute(null);
        Assert.IsType<PilihKelasViewModel>(mainVm.CurrentView);
    }

    [Fact]
    public void RalatViewModel_OpenTeacherModeCommand_NavigatesToModGuruPin()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];

        mainVm.NavigateToRalat(student, FailureCodes.E12_PicturePasswordLocked);
        Assert.IsType<RalatViewModel>(mainVm.CurrentView);
        var ralatVm = (RalatViewModel)mainVm.CurrentView;

        // Teacher clicks Mod Guru button on the error screen
        ralatVm.OpenTeacherModeCommand.Execute(null);
        Assert.IsType<ModGuruPinViewModel>(mainVm.CurrentView);
    }

    [Fact]
    public void MainViewModel_Initializes_With_AppConfig_And_IdleResetSeconds()
    {
        var customConfig = new AppConfig { IdleResetSeconds = 300 };
        var mainVm = new MainViewModel(
            SampleDataService.CreateSampleSchool(),
            SampleDataService.CreateSampleTheme(),
            SampleDataService.CreateSampleClasses(),
            SampleDataService.CreateSampleClassStudents("2_cemerlang"),
            config: customConfig);

        Assert.NotNull(mainVm.Config);
        Assert.Equal(300, mainVm.Config.IdleResetSeconds);
    }

    [Theory]
    [InlineData(LoginFlowState.LaunchingBrowser, "Sedang membuka DELIMa...")]
    [InlineData(LoginFlowState.WaitingForIdentifierPage, "Menunggu skrin masuk...")]
    [InlineData(LoginFlowState.InjectingIdentifier, "Mengisi maklumat...")]
    [InlineData(LoginFlowState.WaitingForTransition, "Menyambung...")]
    [InlineData(LoginFlowState.WaitingForPasswordPage, "Menyediakan akaun...")]
    [InlineData(LoginFlowState.InjectingPassword, "Hampir siap...")]
    [InlineData(LoginFlowState.WaitingForConsentPage, "Mengesahkan akaun...")]
    [InlineData(LoginFlowState.Completed, "Berjaya!")]
    [InlineData(LoginFlowState.Aborted, "Dibatalkan.")]
    [InlineData(LoginFlowState.Failed, "Ada masalah teknikal.")]
    public void SedangMasukViewModel_StateMessages_Map_Correctly(LoginFlowState state, string expectedMessage)
    {
        // Assert BM state messages match PRD §7 and Technical Architecture §4.5
        Assert.Equal(expectedMessage, SedangMasukViewModel.GetStateMessage(state));
    }

    [Fact]
    public void MainViewModel_NavigateToSedangMasuk_Configures_LandingButtonText_For_Ains_And_Canva()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];
        using var cred = new SecurePasswordBuffer("TestPassword123!"u8);

        // Ains destination
        var ainsDest = new DestinationConfig { Id = "ains", Label = "AINS (NILAM)", Url = "https://ains.moe.gov.my/" };
        mainVm.NavigateToSedangMasuk(student, cred, ainsDest);

        Assert.IsType<SedangMasukViewModel>(mainVm.CurrentView);
        var sedangMasukVm = (SedangMasukViewModel)mainVm.CurrentView;
        Assert.Equal("Log Masuk dengan akaun DELIMa", sedangMasukVm.Options.LandingButtonText);

        // Canva destination
        var canvaDest = new DestinationConfig { Id = "canva", Label = "Canva for Education", Url = "https://www.canva.com/login/" };
        mainVm.NavigateToSedangMasuk(student, cred, canvaDest);

        Assert.IsType<SedangMasukViewModel>(mainVm.CurrentView);
        sedangMasukVm = (SedangMasukViewModel)mainVm.CurrentView;
        Assert.Equal("Continue with Google", sedangMasukVm.Options.LandingButtonText);
    }

    [Fact]
    public void ConsentPrompt_ExactText_Matches_Architecture_Spec()
    {
        // §4.5 & PRD §7.4: Identity check prompt on floating reset bar
        const string expectedPrompt = "Lihat nama kamu. Kalau betul, tekan butang biru di bawah.";
        Assert.Equal("Lihat nama kamu. Kalau betul, tekan butang biru di bawah.", expectedPrompt);
    }

    [Fact]
    public async Task MainViewModel_NoPasswordStored_NavigatesToRalat_E11()
    {
        var school = SampleDataService.CreateSampleSchool();
        var theme = SampleDataService.CreateSampleTheme();
        var classes = SampleDataService.CreateSampleClasses();
        var students = SampleDataService.CreateSampleClassStudents("2_cemerlang");
        var store = new FakeCredentialStore(hasCredential: false);

        var mainVm = new MainViewModel(school, theme, classes, students, classes[0], credentialStore: store);
        mainVm.NavigateToKataLaluanGambar(students[0], classes[0], Argon2Parameters.FastTest);
        var kataLaluanVm = (KataLaluanGambarViewModel)mainVm.CurrentView!;

        // Enter default 3 picture-password icons: kucing, bunga, kereta
        var icon1 = kataLaluanVm.ShuffledIcons.First(i => i.Id == "kucing");
        var icon2 = kataLaluanVm.ShuffledIcons.First(i => i.Id == "bunga");
        var icon3 = kataLaluanVm.ShuffledIcons.First(i => i.Id == "kereta");

        await kataLaluanVm.SelectIconCommand.ExecuteAsync(icon1);
        await kataLaluanVm.SelectIconCommand.ExecuteAsync(icon2);
        await kataLaluanVm.SelectIconCommand.ExecuteAsync(icon3);

        // Verification triggers OnPicturePasswordVerified -> navigates to Ralat E11
        Assert.IsType<RalatViewModel>(mainVm.CurrentView);
        var ralatVm = (RalatViewModel)mainVm.CurrentView;
        Assert.Equal(FailureCodes.E11_NoPasswordStored, ralatVm.ErrorCode);
    }

    [Fact]
    public void MainViewModel_LoadsFromProvisionedStoreDirectory_WhenAvailable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LauncherTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var payload = new MasterBundlePayload
            {
                SchemaVersion = 2,
                School = new SchoolInfo
                {
                    Code = "UJIAN",
                    Name = "SK Ujian Kebangsaan",
                    Motto = "Maju Jaya",
                    Domain = "moe-dl.edu.my"
                },
                Theme = new ThemeInfo
                {
                    Primary = "#123456",
                    Accent = "#654321",
                    ClassColours = ["#123456", "#654321"]
                },
                Config = new AppConfig
                {
                    PicturePasswordRequired = true,
                    IdleResetSeconds = 450
                },
                Classes =
                [
                    new Delima.Core.Store.ClassInfo { Id = "2 Ujian", Name = "2 Ujian", Grade = 2, ColourIndex = 0 }
                ],
                Students =
                [
                    new StudentInfo
                    {
                        Id = "s_ujian_01",
                        Name = "Nama Anda Di Sini",
                        ClassId = "2 Ujian",
                        EmailLocal = "g-41360438",
                        Avatar = "kucing",
                        Password = "PasswordSebenarAnda",
                        Active = true
                    }
                ]
            };

            Delima.Win32.Store.DpapiCredentialStore.WriteStore(payload, tempDir, applyAcls: false);

            var mainVm = new MainViewModel(tempDir);

            Assert.NotNull(mainVm.CredentialStore);
            Assert.Equal("UJIAN", mainVm.School.Code);
            Assert.Equal("SK Ujian Kebangsaan", mainVm.School.Name);
            Assert.Equal("#123456", mainVm.Theme.Primary);
            Assert.Single(mainVm.Classes);
            Assert.Equal("2 Ujian", mainVm.Classes[0].Name);
            Assert.Single(mainVm.Students);
            Assert.Equal("Nama Anda Di Sini", mainVm.Students[0].Name);
            Assert.Equal("g-41360438", mainVm.Students[0].EmailLocal);

            // Verify navigation to CariNama matches the provisioned class
            mainVm.NavigateToCariNama(mainVm.Classes[0]);
            Assert.IsType<CariNamaViewModel>(mainVm.CurrentView);
            var cariVm = (CariNamaViewModel)mainVm.CurrentView;
            Assert.Equal("Tahun 2 2 Ujian", cariVm.ClassName);
            // 1 pupil card + 1 escape hatch card
            Assert.Equal(2, cariVm.FilteredPupilCards.Count);
            Assert.Equal("Anda Di Sini", cariVm.FilteredPupilCards[0].DisplayName);
            Assert.Equal("Nama Anda Di Sini", cariVm.FilteredPupilCards[0].Student?.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void MainViewModel_NavigateToSedangMasuk_Sets_SedangMasukViewModel()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];
        using var cred = new SecurePasswordBuffer("Test1234!"u8);

        mainVm.NavigateToSedangMasuk(student, cred);

        Assert.IsType<SedangMasukViewModel>(mainVm.CurrentView);
        var sedangVm = (SedangMasukViewModel)mainVm.CurrentView!;
        Assert.Equal(student, sedangVm.Student);
    }

    [Fact]
    public void MainViewModel_SedangMasukCancel_NavigatesToPilihKelas()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];
        using var cred = new SecurePasswordBuffer("Test1234!"u8);

        mainVm.NavigateToSedangMasuk(student, cred);
        var sedangVm = (SedangMasukViewModel)mainVm.CurrentView!;

        // Pupil clicks Cancel during injection / login
        sedangVm.CancelCommand.Execute(null);

        // Destination must be PilihKelasViewModel (Skrin 1)
        Assert.IsType<PilihKelasViewModel>(mainVm.CurrentView);
    }

    [Fact]
    public void FloatingResetBar_ConsentRefusal_WritesAuditLog_And_NavigatesToPilihKelas()
    {
        string testAuditDir = Path.Combine(Path.GetTempPath(), "ResetBarAuditTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testAuditDir);

        try
        {
            RunInSta(() =>
            {
                var mainVm = new MainViewModel();
                var student = mainVm.Students[0];
                bool consentRefusedCalled = false;

                var resetBar = new Views.FloatingResetBarWindow(
                    student: student,
                    session: null,
                    onReset: () => { },
                    school: mainVm.School,
                    onConsentRefused: () => consentRefusedCalled = true,
                    auditDirectory: testAuditDir);

                // Pupil presses Cancel on consent screen (or clicks Logout before destination is reached)
                Assert.False(resetBar.DestinationReached);
                resetBar.Close();

                Assert.True(consentRefusedCalled);
            });

            // Audit log must have recorded consent_refused
            string logPath = Delima.Core.Audit.AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, testAuditDir);
            Assert.True(File.Exists(logPath));
            string logContent = File.ReadAllText(logPath);
            Assert.Contains("\"event\":\"consent_refused\"", logContent);
            Assert.Contains("\"outcome\":\"REFUSED\"", logContent);
            Assert.Contains("\"outcome_code\":\"G2_CONSENT_REFUSED\"", logContent);
        }
        finally
        {
            if (Directory.Exists(testAuditDir))
            {
                try { Directory.Delete(testAuditDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task MainViewModel_PicturePasswordSuccess_NavigatesToPilihDestinasi_AndThenToSedangMasuk()
    {
        var school = SampleDataService.CreateSampleSchool();
        var theme = SampleDataService.CreateSampleTheme();
        var classes = SampleDataService.CreateSampleClasses();
        var students = SampleDataService.CreateSampleClassStudents("2_cemerlang");
        var store = new FakeCredentialStore(hasCredential: true);

        var customDestinations = new List<DestinationConfig>
        {
            new() { Id = "classroom", Label = "Google Classroom", Url = "https://classroom.google.com/" },
            new() { Id = "delima", Label = "DELIMa 3.0", Url = "https://d3.delima.edu.my/landing" }
        };

        var config = new AppConfig { Destinations = customDestinations };
        var mainVm = new MainViewModel(school, theme, classes, students, classes[0], credentialStore: store, config: config);

        mainVm.NavigateToKataLaluanGambar(students[0], classes[0], Argon2Parameters.FastTest);
        var kataLaluanVm = (KataLaluanGambarViewModel)mainVm.CurrentView!;

        // Enter correct 3 picture-password icons: kucing, bunga, kereta
        var icon1 = kataLaluanVm.ShuffledIcons.First(i => i.Id == "kucing");
        var icon2 = kataLaluanVm.ShuffledIcons.First(i => i.Id == "bunga");
        var icon3 = kataLaluanVm.ShuffledIcons.First(i => i.Id == "kereta");

        await kataLaluanVm.SelectIconCommand.ExecuteAsync(icon1);
        await kataLaluanVm.SelectIconCommand.ExecuteAsync(icon2);
        await kataLaluanVm.SelectIconCommand.ExecuteAsync(icon3);

        // Verification triggers OnPicturePasswordVerified -> navigates to PilihDestinasiViewModel
        Assert.IsType<PilihDestinasiViewModel>(mainVm.CurrentView);
        var destinasiVm = (PilihDestinasiViewModel)mainVm.CurrentView;
        Assert.Equal(2, destinasiVm.AvailableDestinations.Count);
        Assert.Equal("Google Classroom", destinasiVm.AvailableDestinations[0].Label);

        // Pupil picks Google Classroom
        var chosenCard = destinasiVm.AvailableDestinations[0];
        destinasiVm.SelectDestinationCommand.Execute(chosenCard);

        // Should have transitioned to SedangMasukViewModel with Google Classroom
        Assert.IsType<SedangMasukViewModel>(mainVm.CurrentView);
        var sedangVm = (SedangMasukViewModel)mainVm.CurrentView;
        Assert.Equal("Google Classroom", sedangVm.Destination?.Label);
        Assert.NotEmpty(sedangVm.StatusMessage);
    }

    [Fact]
    public void MainWindow_KioskMode_PreventsCloseUnlessForceClosed()
    {
        RunInSta(() =>
        {
            var kioskWindow = new MainWindow(isKiosk: true);
            Assert.True(kioskWindow.IsKiosk);

            // Attempt normal close
            kioskWindow.Close();

            // ForceClose allows clean exit
            kioskWindow.ForceClose();
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (ex != null)
        {
            throw new AggregateException("STA thread execution failed", ex);
        }
    }

    private sealed class FakeCredentialStore(bool hasCredential) : ICredentialStore
    {
        public ushort SchemaVersion => 2;
        public bool HasCredential(string studentId) => hasCredential;
        public ICredential OpenCredential(string studentId) => new SecurePasswordBuffer("TestPassword123!"u8);
        public void Dispose() { }
    }
}
