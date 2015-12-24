using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FileDateOffset
{
    public partial class FrmFileDateOffset : Form
    {
        public string InitialPath
        {
            get { return txtPath.Text; }
            set { txtPath.Text = value; }
        }

        public FrmFileDateOffset()
        {
            InitializeComponent();
            txtPath.Text = Directory.GetCurrentDirectory();
            cbFilter_Init();
        }

        #region Filter

        private void cbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtFilter.Enabled = (cbFilter.SelectedIndex == 4);
        }

        private void cbFilter_Init()
        {
            this.cbFilter.Items.AddRange(new object[] {
                "All",
                "DSC_ + MOV_",
                "IMG_ + VID_",
                "DSC_ + MOV_ + IMG_ + VID_",
                "Custom"});
            cbFilter.SelectedIndex = 0;
            txtFilter.Enabled = false;
        }

        private bool cbFilter_ApplyFilter(string fileName)
        {
            switch (cbFilter.SelectedIndex)
            {
                case 0:
                    return true;
                case 1:
                    return (fileName.StartsWith("DSC_") || fileName.StartsWith("MOV_"));
                case 2:
                    return (fileName.StartsWith("IMG_") || fileName.StartsWith("VID_"));
                case 3:
                    return (fileName.StartsWith("DSC_") || fileName.StartsWith("IMG_") || fileName.StartsWith("MOV_") || fileName.StartsWith("VID_"));
                case 4:
                    return txtFilter_ApplyFilter(fileName);
            }
            return false;
        }

        private bool txtFilter_ApplyFilter(string fileName)
        {
            if (string.IsNullOrEmpty(txtFilter.Text))
            {
                return false;
            }

            string[] filters = txtFilter.Text.Split('|');

            foreach (string strFilter in filters)
            {
                if (fileName.Contains(strFilter))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region lsbFiles

        private void lsbFiles_Clean()
        {
            lsbFiles.Items.Clear();
        }

        private void lsbFiles_AddLine(string line)
        {
            lsbFiles.Items.Add(line);

            Application.DoEvents();

            int visibleItems = lsbFiles.ClientSize.Height / lsbFiles.ItemHeight;
            lsbFiles.TopIndex = Math.Max(lsbFiles.Items.Count - visibleItems + 1, 0);
        }

        #endregion

        #region CreationDate

        /// <summary>
        /// Returns the EXIF Image Data of the Date Taken.
        /// </summary>
        /// <param name="getImage">Image (If based on a file use Image.FromFile(f);)</param>
        /// <returns>Date Taken or Null if Unavailable</returns>
        public static DateTime? GetExifDateTaken(Image image)
        {
            int DateTakenValue = 0x9003; //36867;

            if (!image.PropertyIdList.Contains(DateTakenValue))
                return null;

            string dateTakenTag = Encoding.ASCII.GetString(image.GetPropertyItem(DateTakenValue).Value);
            string[] parts = dateTakenTag.Split(':', ' ');
            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);
            int hour = int.Parse(parts[3]);
            int minute = int.Parse(parts[4]);
            int second = int.Parse(parts[5]);

            return new DateTime(year, month, day, hour, minute, second);
        }

        public static void SetExifDateTaken(Image image, DateTime dtTaken)
        {
            int DateTakenValue = 0x9003; //36867;

            if (!image.PropertyIdList.Contains(DateTakenValue))
                return;

            string strDateTaken = string.Format("{0}:{1}:{2} {3}:{4}:{5}\0", dtTaken.Year, dtTaken.Month, dtTaken.Day,
                                                dtTaken.Hour, dtTaken.Minute, dtTaken.Second);
            byte[] bytesDateTaken = Encoding.ASCII.GetBytes(strDateTaken);
            PropertyItem prop = image.GetPropertyItem(DateTakenValue);
            prop.Len = bytesDateTaken.Length;
            prop.Value = bytesDateTaken;
            image.RemovePropertyItem(DateTakenValue);
            image.SetPropertyItem(prop);
        }

        public static DateTime GetFileCreationDate(string filePath)
        {
            DateTime dtFile;

            DateTime dtCretation = File.GetCreationTime(filePath);
            DateTime dtLastMod = File.GetLastWriteTime(filePath);
            if (dtCretation < dtLastMod)
            {
                dtFile = dtCretation;
            }
            else
            {
                dtFile = dtLastMod;
            }

            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif")
            {
                Image img = Image.FromFile(filePath);
                DateTime? dtTaken = GetExifDateTaken(img);
                if (dtTaken != null)
                {
                    dtFile = (DateTime)dtTaken;
                }
                img.Dispose();
            }

            return dtFile;
        }

        public static void SetFileCreationDate(string filePath, DateTime dtCreation, bool setImageDate)
        {
            if (setImageDate)
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif")
                {
                    Image img = Image.FromFile(filePath);
                    SetExifDateTaken(img, dtCreation);
                    string filePath2 = string.Format("{0}.temp.jpg", filePath);
                    img.Save(filePath2);
                    img.Dispose();
                    File.Delete(filePath);
                    File.Move(filePath2, filePath);
                }
            }

            File.SetCreationTime(filePath, dtCreation);
            File.SetLastWriteTime(filePath, dtCreation);
        }
        
        #endregion
        
        public class DateOffset
        {
            public int Years = 0;
            public int Months = 0;
            public int Days = 0;
            public int Hours = 0;
            public int Minutes = 0;
            public int Seconds = 0;
        }

        public int ParseInt(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                int temp;
                if (int.TryParse(text, out temp))
                {
                    return temp;
                }
            }
            return 0;
        }

        public DateOffset GetDateOffset()
        {
            var offset = new DateOffset
                {
                    Years = ParseInt(txtYears.Text),
                    Months = ParseInt(txtMonths.Text),
                    Days = ParseInt(txtDays.Text),
                    Hours = ParseInt(txtHours.Text),
                    Minutes = ParseInt(txtMinutes.Text),
                    Seconds = ParseInt(txtSeconds.Text)
                };
            return offset;
        }

        private void ProcessFile_ApplyOffset(string filePath, string fileName, bool doAction, DateOffset dateOffset)
        {
            DateTime dtFile = GetFileCreationDate(filePath);
            if (doAction)
            {
                dtFile =
                    dtFile.AddSeconds(dateOffset.Seconds)
                          .AddMinutes(dateOffset.Minutes)
                          .AddHours(dateOffset.Hours)
                          .AddDays(dateOffset.Days)
                          .AddMonths(dateOffset.Months)
                          .AddYears(dateOffset.Years);
                SetFileCreationDate(filePath, dtFile, true);
            }

            lsbFiles_AddLine(string.Format("ApplyOffset: {0} {1} ", fileName, dtFile.ToString()));
        }

        private void ProcessFile_SetDate(string filePath, string fileName, bool doAction, DateOffset dateNew)
        {
            DateTime dtFile = new DateTime(dateNew.Years, dateNew.Months, dateNew.Days, dateNew.Hours, dateNew.Minutes, dateNew.Seconds);
            if (doAction)
            {
                SetFileCreationDate(filePath, dtFile, false);
            }

            lsbFiles_AddLine(string.Format("SetDate: {0} {1} ", fileName, dtFile.ToString()));
        }

        private void ProcessDirectory(string path, bool doAction, DateOffset dateOffset, bool setDate)
        {
            string[] filePaths = Directory.GetFiles(path);
            foreach (string filePath in filePaths)
            {
                string fileName = Path.GetFileName(filePath);

                if (cbFilter_ApplyFilter(fileName))
                {
                    if (setDate)
                    {
                        ProcessFile_SetDate(filePath, fileName, doAction, dateOffset);
                    }
                    else
                    {
                        ProcessFile_ApplyOffset(filePath, fileName, doAction, dateOffset);
                    }
                }
                else
                {
                    lsbFiles_AddLine(string.Format("Unchanged: {0}", fileName));
                }
            }

            string[] directoryPaths = Directory.GetDirectories(path);
            foreach (string directoryPath in directoryPaths)
            {
                ProcessDirectory(directoryPath, doAction, dateOffset, setDate);
            }
        }

        private void Process(bool doAction)
        {
            string path = txtPath.Text;
            if (!Directory.Exists(path))
            {
                MessageBox.Show("El directorio no existe");
                return;
            }

            lsbFiles_Clean();
            DateOffset dateOffset = GetDateOffset();
            ProcessDirectory(path, doAction, dateOffset, chkSetDate.Checked);
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            Process(true);
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            Process(false);
        }

        private void btnSetNow_Click(object sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;

            txtYears.Text = Convert.ToString(now.Year);
            txtMonths.Text = Convert.ToString(now.Month);
            txtDays.Text = Convert.ToString(now.Day);
            txtHours.Text = Convert.ToString(now.Hour);
            txtMinutes.Text = Convert.ToString(now.Minute);
            txtSeconds.Text = Convert.ToString(now.Second);
        }
    }
}
