using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace AutoProtonCreator
{
    public partial class Form1 : Form
    {
        private IWebDriver driver;
        private static List<string> wordList = new List<string>();
        private string lastGeneratedPassword = "";

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_Z = 0x5A; // 'Z'

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public Form1()
        {
            InitializeComponent();

            // Register Ctrl + Alt + Z hotkey
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_Z);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                this.Invoke(new Action(() =>
                {
                    if (this.WindowState == FormWindowState.Minimized)
                        this.WindowState = FormWindowState.Normal;

                    this.Activate();
                    this.BringToFront();
                    this.TopMost = true;  // briefly bring on top
                    this.TopMost = false;
                    this.Focus();
                }));
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosing(e);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadWordsFromFile("words.txt");

            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");

            // Get path to the folder where your .exe (and chromedriver.exe) will be
            string driverPath = AppDomain.CurrentDomain.BaseDirectory;

            var driverService = ChromeDriverService.CreateDefaultService(driverPath);
            driverService.HideCommandPromptWindow = true;

            driver = new ChromeDriver(driverService, options);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        }

        private static void LoadWordsFromFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    wordList = File.ReadAllLines(path)
                        .Select(w => w.Trim().ToLower())
                        .Where(w => w.Length >= 8)
                        .ToList();
                }
                else
                {
                    MessageBox.Show("Word file not found: " + path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading word file: " + ex.Message);
            }
        }

        private static string GenerateUsername()
        {
            var rand = new Random();
            if (wordList.Count < 2)
                return "user." + GetShortTimestamp();

            string word1 = wordList[rand.Next(wordList.Count)];
            string timestamp = GetShortTimestamp();
            return $"{word1}.{timestamp}";
        }

        private static string GeneratePassword()
        {
            var rand = new Random();
            if (wordList.Count < 1)
                return "Password123!";

            string word1 = wordList[rand.Next(wordList.Count)];
            string word2 = wordList[rand.Next(wordList.Count)];

            while (word2 == word1 && wordList.Count > 1)
                word2 = wordList[rand.Next(wordList.Count)];

            int number = rand.Next(10, 100);
            char[] symbols = { '!', '@', '#', '$', '%', '&' };
            char symbol = symbols[rand.Next(symbols.Length)];

            return $"{word1}{word2}{number}{symbol}";
        }

        private static string GetShortTimestamp()
        {
            long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            byte[] bytes = BitConverter.GetBytes(unixTime);
            string base64 = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', 'u')
                .Replace('/', 'x');
            return base64.Substring(0, 4);
        }

        private void SaveCredentials(string username, string password)
        {
            string line = $"{username}:{password}";
            File.AppendAllText("accounts.txt", line + Environment.NewLine);
        }

        private IWebElement WaitForElement(string cssSelector, int timeoutSeconds = 10)
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(d =>
            {
                try
                {
                    var el = d.FindElement(By.CssSelector(cssSelector));
                    return (el.Displayed && el.Enabled) ? el : null;
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
            });
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            CreateAccount();
        }

        private void CreateAccount()
        {
            try
            {
                driver.Navigate().GoToUrl("https://account.proton.me/mail/signup");

                string username = GenerateUsername();
                lastGeneratedPassword = GeneratePassword();

                var freePlanButton = WaitForElement("div.plan-card-selector-container button");
                freePlanButton?.Click();

                var iframe = WaitForElement("form iframe");
                iframe?.Click();

                var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                actions.SendKeys(username).Perform();

                var passwordBox = WaitForElement("body > div.app-root > div.flex.\\*\\:min-size-auto.flex-nowrap.flex-column.h-full.overflow-auto.relative.signup-v2-wrapper.signup-v2-bg.signup-v2-bg--mail > div > div:nth-child(1) > div.single-box.relative.mt-12.w-full.max-w-custom > div.pricing-box-content.mt-8 > div > div.flex-1.w-0.relative > form > div.flex.flex-column.mb-4 > div.field-two-container.field-two--dense.mt-2.pb-2 > div.field-two-input-container.relative");
                passwordBox?.Click();
                actions.SendKeys(lastGeneratedPassword).Perform();

                var confirmBox = WaitForElement("form div:nth-child(4) div.field-two-input-container");
                confirmBox?.Click();
                actions.SendKeys(lastGeneratedPassword).Perform();

                var startButton = WaitForElement("form > div:nth-child(3) > button");
                startButton?.Click();

                var xButton = WaitForElement("body > div.modal-two > dialog > div > div.flex.mb-4.items-center.justify-center > div:nth-child(3) > button");
                xButton?.Click();

                try
                {
                    var continueButton = WaitForElement("form button[type='submit']", 240);
                    continueButton?.Click();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Continue button not found or clickable: " + ex.Message);
                    return;
                }

                try
                {
                    var maybeLaterButton = WaitForElement("form button.button-ghost-norm", 120);
                    maybeLaterButton?.Click();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Maybe later button not found: " + ex.Message);
                }

                try
                {
                    var confirmButton = WaitForElement("dialog button.button-solid-norm", 120);
                    confirmButton?.Click();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Confirm button not found: " + ex.Message);
                }

                try
                {
                    // 1. Click "Let's get started"
                    var letsGetStarted = WaitForElement("div[id^='modal-'][id$='description'] > footer > button", 30);
                    letsGetStarted?.Click();
                    System.Threading.Thread.Sleep(500);

                    // 2. Click "Maybe later"
                    var maybeLater = WaitForElement("div[id^='modal-'][id$='description'] > footer > button", 120);
                    maybeLater?.Click();
                    System.Threading.Thread.Sleep(500);

                    // 3. Click "Next"
                    var nextButton = WaitForElement("div[id^='modal-'][id$='description'] div > div > footer > button", 120);
                    nextButton?.Click();
                    System.Threading.Thread.Sleep(500);

                    // 4. Click "Use this"
                    var useThis = WaitForElement("div[id^='modal-'][id$='description'] > footer > button", 120);
                    useThis?.Click();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error during final onboarding steps: " + ex.Message);
                }

                SaveCredentials(username, lastGeneratedPassword);

                //Logout
                //Click profile menu
                var profileMenu = WaitForElement("body > div.app-root > div.flex.flex-row.flex-nowrap.h-full > div > div > div > div.flex.flex-column.flex-1.flex-nowrap.reset4print > div > div > header > div.topnav-container.flex.flex-none.md\\:flex-initial.justify-end.self-center.my-auto.ml-auto.md\\:ml-0.no-print > ul > li.topnav-listItem.relative.hidden.md\\:flex > button");
                profileMenu?.Click();

                //Click logout
                var logoutButton = WaitForElement("div.dropdown-content > div > div.mb-4.px-4.flex.flex-column.gap-2 > button");
                logoutButton?.Click();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating account: " + ex.Message);
            }
        }
    }
}
