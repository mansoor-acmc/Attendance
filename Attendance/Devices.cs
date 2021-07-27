using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BAL;
using BioStationAPI;

namespace Attendance
{
    public partial class Devices : Form
    {
        public Devices()
        {
            InitializeComponent();


        }

        private void Devices_Load(object sender, EventArgs e)
        {
            BioStationAPI.BioAttendance objBiostation = new BioStationAPI.BioAttendance(null,null);
            var devices = objBiostation.Init_Get_Devices();
            List<BS2User> users = null;// objBiostation.GetUsersInDevices(devices);

            dgvUsers.DataSource = users;
            
        }
    }
}
