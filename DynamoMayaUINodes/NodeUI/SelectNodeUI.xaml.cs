using System;
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
using DynaMaya.Nodes.NodeUI;


namespace DynaMaya.NodeUI
{
    /// <summary>
    /// Interaction logic for SelectNodeUI.xaml
    /// </summary>
    public partial class SelectNodeUI : UserControl
    {
        private OptionPanel optPanel;
        private bool doDiffer = false;
        private Brush differActive;
        private Brush differInactive;
   
        public SelectNodeUI()
        {
            InitializeComponent();
            optPanel = new OptionPanel();
            optPanel.Visibility = Visibility.Hidden;
            optPanel.Margin = new Thickness(0, 0, 0, 0);
           
            SelectNodeStackPnl.Children.Add(optPanel);
            
    
        }

 

        private void button1_Click(object sender, RoutedEventArgs e)
        {

            if (optPanel.IsVisible)
            {
                selectNodePanel.Height = 50;
                selectNodePanel.Width = 120;
                optPanel.Visibility = Visibility.Hidden;
    
            }
            else
            {
                optPanel.Visibility = Visibility.Visible;
                selectNodePanel.Height = 150;
                selectNodePanel.Width = 120;
      
            }
        }

        private void btDiffer_Click(object sender, RoutedEventArgs e)
        {
            if (doDiffer)
            {
                doDiffer = false;
                btUpdate.IsEnabled = false;


            }
            else
            {
                doDiffer = true;
                btUpdate.IsEnabled = true;

            }
        }
    }
}
