﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EmailClient;

namespace IMAPImplementation.Views
{
    /// <summary>
    /// Interaction logic for MailBoxView.xaml
    /// </summary>
    public partial class MailBoxView : UserControl, IDisposable
    {
        IMAPClient m_client = null;
        public MailBoxView(IMAPClient client)
        {
            m_client = client;
            this.DataContext = new MailBoxViewModel(client);
            InitializeComponent();
        }

        public void Dispose()
        {
            m_client.Disconnect();
        }
    }
}
