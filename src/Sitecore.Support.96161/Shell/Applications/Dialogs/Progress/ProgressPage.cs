namespace Sitecore.Support.Shell.Applications.Dialogs.Progress
{
    using Sitecore;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Jobs;
    using Sitecore.Resources;
    using Sitecore.Shell.Framework.Jobs;
    using Sitecore.Web.UI;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.Web.UI.XamlSharp.Xaml;
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;

    public class ProgressPage : XamlMainControl
    {
        protected Button Close;
        protected Literal HeaderText;
        protected Literal Log;
        protected ThemedImage MoreImage;
        protected Literal MoreInformation;
        protected Border MoreInformationContainer;
        protected Border Progress;
        protected Image ProgressSpacer;
        protected Literal Subtitle;
        protected Literal Title;
        protected ThemedImage TitleIcon;

        //The CheckStatus method was rewritten in the compatible way with load-balanced environment
        //Other methods were not modified anyhow
        protected void CheckStatus()
        {
            if (this.Handle.IsLocal)
            {
                Job job = JobManager.GetJob(this.Handle);
                Assert.IsNotNull(job, "job in checkstatus");
                if (job.Status.State == JobState.Finished)
                {
                    this.UpdateFinished(job);
                }
                else if (job.Status.Total <= 0L)
                {
                    SheerResponse.Timer("CheckStatus", 0x3e8);
                    this.UpdateStatus(job);
                }
                else
                {
                    string factor = ((double)(((float)job.Status.Processed) / ((float)job.Status.Total))).ToString("0.00", CultureInfo.InvariantCulture);
                    this.UpdateFactor(factor);
                    this.UpdateStatus(job);
                    SheerResponse.Timer("CheckStatus", 500);
                }
            }
            else
            {
                SheerResponse.Timer("CheckStatus", 500);
            }
        }

        private string Clip(string text, int limit)
        {
            Assert.IsNotNull(text, "text");
            if (text.Length > limit)
            {
                text = text.Substring(0, limit) + "...";
            }
            return text;
        }

        protected void Close_Click()
        {
            Job job = this.GetJob();
            if (job != null)
            {
                job.Status.Expiry = DateTime.UtcNow.AddMinutes(1.0);
            }
            SheerResponse.SetDialogValue("Manual close");
            SheerResponse.CloseWindow();
        }

        private Job GetJob() =>
            JobManager.GetJob(this.Handle);

        private string GetLastJobErrorMessage(Job job, out bool bIsExceptionMsg)
        {
            Assert.ArgumentNotNull(job, "job");
            bIsExceptionMsg = false;
            if (job.Status.Messages.Count == 0)
            {
                return "An error occured";
            }
            string[] strArray = new string[] { Translate.Text("#Exception: "), Translate.Text("#Error: ") };
            for (int i = job.Status.Messages.Count - 1; i >= 0; i--)
            {
                string str = job.Status.Messages[i];
                bIsExceptionMsg = str.StartsWith(strArray[0], StringComparison.OrdinalIgnoreCase);
                if (bIsExceptionMsg)
                {
                    return StringUtil.RemovePrefix(strArray[0], str);
                }
                if (str.StartsWith(strArray[1], StringComparison.OrdinalIgnoreCase))
                {
                    return StringUtil.RemovePrefix(strArray[1], str);
                }
            }
            return job.Status.Messages[job.Status.Messages.Count - 1];
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!XamlControl.AjaxScriptManager.IsEvent)
            {
                this.MoreInformation.Text = Translate.Text("View all messages");
                LongRunningOptions options = LongRunningOptions.Parse();
                this.Handle = Sitecore.Handle.Parse(options.Handle);
                this.HeaderText.Text = options.Title;
                Assert.IsNotNull(JobManager.GetJob(this.Handle), "job");
            }
        }

        private void ShowException(Job job)
        {
            string lastJobErrorMessage = "An error occured";
            if (job.Status.Messages.Count > 0)
            {
                bool flag;
                lastJobErrorMessage = this.GetLastJobErrorMessage(job, out flag);
            }
            lastJobErrorMessage = "<h2> <i>An error occured</i></h2><br />" + lastJobErrorMessage;
            SheerResponse.SetAttribute("ErrorMessage", "value", lastJobErrorMessage);
            SheerResponse.Eval("showException()");
        }

        protected void ToggleInformation()
        {
            Job job = this.GetJob();
            Assert.IsNotNull(job, "job");
            if (job.Status.Failed)
            {
                this.ShowException(job);
            }
            else
            {
                this.MoreInformation.Text = this.Expanded ? Translate.Text("View all messages") : Translate.Text("Hide messages");
                this.Expanded = !this.Expanded;
                SheerResponse.Eval("toggle()");
            }
        }

        private void UpdateFactor(string factor)
        {
            Assert.ArgumentNotNullOrEmpty(factor, "factor");
            SheerResponse.Eval(new ScriptInvokationBuilder("progressTo").AddString(factor, new object[0]).ToString());
        }

        private void UpdateFinished(Job job)
        {
            Assert.ArgumentNotNull(job, "job");
            if (job.Status.Failed)
            {
                bool flag;
                TimeSpan span = (TimeSpan)(DateTime.UtcNow - job.Status.Expiry);
                if (span.TotalMinutes < 30.0)
                {
                    job.Status.Expiry = DateTime.UtcNow.AddMinutes(30.0);
                }
                this.Progress.Visible = false;
                this.ProgressSpacer.Visible = false;
                this.Title.Text = "An error occured";
                this.Title.Class = "error";
                this.TitleIcon.Visible = true;
                this.MoreImage.Src = Images.GetThemedImageSource("Office/16x16/clipboard_paste.png", ImageDimension.id16x16);
                this.MoreImage.Class = "error";
                string lastJobErrorMessage = this.GetLastJobErrorMessage(job, out flag);
                this.Subtitle.Text = StringUtil.Clip(lastJobErrorMessage, 120, true);
                this.Subtitle.Visible = true;
                this.MoreInformationContainer.Visible = flag;
                if (flag)
                {
                    this.MoreInformation.Text = "View error";
                }
                this.Close.Visible = true;
            }
            else
            {
                SheerResponse.SetDialogValue("Finished");
                SheerResponse.CloseWindow();
            }
        }

        private void UpdateStatus(Job job)
        {
            Assert.ArgumentNotNull(job, "job");
            string str = string.Empty;
            StringCollection messages = job.Status.Messages;
            if (messages.Count > 0)
            {
                str = messages[messages.Count - 1];
            }
            this.Title.Text = str;
            SheerResponse.SetAttribute("Title", "title", str);
            if (this.Expanded)
            {
                string[] strArray;
                lock (messages)
                {
                    strArray = new string[messages.Count - this.LastUpdatedMessageIndex];
                    for (int i = this.LastUpdatedMessageIndex; i < messages.Count; i++)
                    {
                        strArray[i - this.LastUpdatedMessageIndex] = messages[i];
                    }
                }
                this.LastUpdatedMessageIndex = messages.Count;
                SheerResponse.Eval(new ScriptInvokationBuilder("appendLog").AddString(strArray.Aggregate<string, string>(string.Empty, (s, s1) => s + s1 + "<br />"), new object[0]).ToString());
            }
        }

        protected bool Expanded
        {
            get
            {
                return MainUtil.GetBool(this.ViewState["Expanded"], false);
            }

            set
            {
                this.ViewState["Expanded"] = value ? "1" : "0";
            }
        }

        protected Sitecore.Handle Handle
        {
            get
            {
                return Sitecore.Handle.Parse(StringUtil.GetString(this.ViewState["Handle"]));
            }

            set
            {
                Assert.ArgumentNotNull(value, "value");
                this.ViewState["Handle"] = value.ToString();
            }
        }

        protected int LastUpdatedMessageIndex
        {
            get
            {
                return MainUtil.GetInt(this.ViewState["LastUpdatedMessageIndex"], 0);
            }

            set
            {
                this.ViewState["LastUpdatedMessageIndex"] = value;
            }
        }
    }
}
