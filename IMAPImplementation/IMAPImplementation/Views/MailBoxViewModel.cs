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
    public class MailBoxViewModel
    {
        IMAPClient m_IMAPClient;
        public MailBoxViewModel(IMAPClient client)
        {
            m_IMAPClient = client;

            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
                return;

            Inboxes = new ObservableCollection<EmailInbox>(client.GetInboxList());
        }

        public ObservableCollection<EmailInbox> Inboxes { get; set; }
    }
}
