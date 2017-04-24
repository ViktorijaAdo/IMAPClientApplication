using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmailClient;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace IMAPImplementation.Views
{
    public class MailBoxViewModel : INotifyPropertyChanged
    {
        private IMAPClient m_IMAPClient;
        private EmailInbox m_selectedInbox;
        private Email m_selectedEmail;

        public event PropertyChangedEventHandler PropertyChanged;

        public MailBoxViewModel(IMAPClient client)
        {
            m_IMAPClient = client;

            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
                return;

            Inboxes = new ObservableCollection<EmailInbox>(client.GetInboxList());
        }

        public ObservableCollection<EmailInbox> Inboxes { get; private set; }

        public EmailInbox SelectedInbox
        {
            get
            {
                return m_selectedInbox;
            }
            set
            {
                m_selectedInbox = value;
                UpdateEmailList();
            }
        }

        public ObservableCollection<Email> EmailList { get; private set; }

        public Email SelectedEmail
        {
            get
            {
                return m_selectedEmail;
            }
            set
            {
                m_selectedEmail = value;
                UpdateEmailText();
            }
        }

        private void UpdateEmailList()
        {
            EmailList = new ObservableCollection<Email>(m_IMAPClient.GetEmailList(m_selectedInbox.Name));
            PropertyChanged(this, new PropertyChangedEventArgs("EmailList"));
        }

        public void UpdateEmailText()
        {
            m_IMAPClient.GetUpdateEmailWithText(ref m_selectedEmail);
            PropertyChanged(this, new PropertyChangedEventArgs("SelectedEmail"));
        }
    }
}
