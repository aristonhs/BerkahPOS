﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using MySql.Data;
using MySql.Data.MySqlClient;
using Hotkeys;


namespace ALPHASOFT_BERKAH
{
    public partial class dataGroupDetailForm : Form
    {
        private int originModuleID = 0;
        private int selectedGroupID = 0;

        private Data_Access DS = new Data_Access();
        private globalUtilities gutil = new globalUtilities();
        private int options = 0;

        private groupAccessModuleForm parentForm;

        private Hotkeys.GlobalHotkey ghk_UP;
        private Hotkeys.GlobalHotkey ghk_DOWN;

        public dataGroupDetailForm()
        {
            InitializeComponent();
        }

        public dataGroupDetailForm(int moduleID)
        {
            InitializeComponent();
            originModuleID = moduleID;
        }

        public dataGroupDetailForm(int moduleID, int groupID)
        {
            InitializeComponent();

            originModuleID = moduleID;
            selectedGroupID = groupID;
        }

        public dataGroupDetailForm(int moduleID, groupAccessModuleForm originForm)
        {
            InitializeComponent();
            originModuleID = moduleID;
            parentForm = originForm;
        }

        private void captureAll(Keys key)
        {
            switch (key)
            {
                case Keys.Up:
                    SendKeys.Send("+{TAB}");
                    break;
                case Keys.Down:
                    SendKeys.Send("{TAB}");
                    break;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Constants.WM_HOTKEY_MSG_ID)
            {
                Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                int modifier = (int)m.LParam & 0xFFFF;

                if (modifier == Constants.NOMOD)
                    captureAll(key);
            }

            base.WndProc(ref m);
        }

        private void registerGlobalHotkey()
        {
            ghk_UP = new Hotkeys.GlobalHotkey(Constants.NOMOD, Keys.Up, this);
            ghk_UP.Register();

            ghk_DOWN = new Hotkeys.GlobalHotkey(Constants.NOMOD, Keys.Down, this);
            ghk_DOWN.Register();         
        }

        private void unregisterGlobalHotkey()
        {
            ghk_UP.Unregister();
            ghk_DOWN.Unregister();
        }

        private void loadUserGroupDataInformation()
        {
            MySqlDataReader rdr;
            DataTable dt = new DataTable();

            DS.mySqlConnect();

            using (rdr = DS.getData("SELECT * FROM MASTER_GROUP WHERE GROUP_ID = "+selectedGroupID))
            {
                if (rdr.HasRows)
                {
                    while (rdr.Read())
                    { 
                        namaGroupTextBox.Text = rdr.GetString("GROUP_USER_NAME");
                        deskripsiTextBox.Text = rdr.GetString("GROUP_USER_DESCRIPTION");

                        if (rdr.GetString("GROUP_USER_ACTIVE").Equals("1"))
                            nonAktifCheckbox.Checked = false;
                        else
                            nonAktifCheckbox.Checked = true;
                    }
                }
            }
        }

        private void dataGroupDetailForm_Load(object sender, EventArgs e)
        {
            Button[] arrButton = new Button[2];

            arrButton[0] = SaveButton;
            arrButton[1] = ResetButton;
            gutil.reArrangeButtonPosition(arrButton, SaveButton.Top, this.Width);

            gutil.reArrangeTabOrder(this);
            namaGroupTextBox.Select();
        }

        private bool dataValidated()
        {
            if (namaGroupTextBox.Text.Trim().Equals(""))
            {
                errorLabel.Text = "NAMA GROUP TIDAK BOLEH KOSONG";
                return false;
            }

            return true;
        }

        private bool saveData()
        {
            bool result = false;
            if (dataValidated())
            {
                smallPleaseWait pleaseWait = new smallPleaseWait();
                pleaseWait.Show();

                //  ALlow main UI thread to properly display please wait form.
                Application.DoEvents();
                result = saveDataTransaction();

                pleaseWait.Close();

                return result;
            }

            return result;
        }

        private bool saveDataTransaction()
        {
            bool result = false;
            string sqlCommand = "";
            MySqlException internalEX = null;

            string groupName = MySqlHelper.EscapeString(namaGroupTextBox.Text.Trim());
            string groupDesc = MySqlHelper.EscapeString(deskripsiTextBox.Text.Trim());
            byte groupStatus= 0;
            byte groupCashier = 0;

            if (nonAktifCheckbox.Checked)
                groupStatus = 0;
            else
                groupStatus = 1;

            if (cashierCheckBox.Checked)
                groupCashier = 1;
            else
                groupCashier = 0;

            DS.beginTransaction();

            try
            {
                DS.mySqlConnect();

                switch(originModuleID)
                {
                    case globalConstants.NEW_GROUP_USER:
                    case globalConstants.PENGATURAN_GRUP_AKSES:
                        sqlCommand = "INSERT INTO MASTER_GROUP (GROUP_USER_NAME, GROUP_USER_DESCRIPTION, GROUP_USER_ACTIVE, GROUP_IS_CASHIER) VALUES ('" + groupName + "', '" + groupDesc + "', " + groupStatus + ", " + groupCashier + ")";
                        break;
                    case globalConstants.EDIT_GROUP_USER:
                        sqlCommand = "UPDATE MASTER_GROUP SET GROUP_USER_NAME = '" + groupName + "', GROUP_USER_DESCRIPTION = '" + groupDesc + "', GROUP_USER_ACTIVE = " + groupStatus + ", GROUP_IS_CASHIER = " + groupCashier + " WHERE GROUP_ID = " + selectedGroupID;
                        break;
                }

                if (!DS.executeNonQueryCommand(sqlCommand, ref internalEX))
                    throw internalEX;

                DS.commit();
                result = true;
            }
            catch (Exception e)
            {
                try
                {
                    DS.rollBack();
                }
                catch (MySqlException ex)
                {
                    if (DS.getMyTransConnection() != null)
                    {
                        gutil.showDBOPError(ex, "ROLLBACK");
                    }
                }

                gutil.showDBOPError(e, "INSERT");
                result = false;
            }
            finally
            {
                if (originModuleID == globalConstants.PENGATURAN_GRUP_AKSES && result == true)
                {
                    selectedGroupID = Convert.ToInt32(DS.getDataSingleValue("SELECT MAX(GROUP_ID) FROM MASTER_GROUP WHERE GROUP_USER_NAME = '" + groupName + "' AND GROUP_USER_DESCRIPTION = '"+groupDesc+"' "));
                    parentForm.setSelectedGroupID(selectedGroupID);
                }

                DS.mySqlClose();
            }

            return result;
        }
        
        private void dataGroupDetailForm_Activated(object sender, EventArgs e)
        {
            switch (originModuleID)
            {
                case (globalConstants.NEW_GROUP_USER):
                case (globalConstants.PENGATURAN_GRUP_AKSES):
                    options = gutil.INS;
                    nonAktifCheckbox.Enabled = false;
                    break;
                case (globalConstants.EDIT_GROUP_USER):
                    options = gutil.UPD;
                    nonAktifCheckbox.Enabled = true;
                    loadUserGroupDataInformation();
                    break;
            }            
            errorLabel.Text = "";

            registerGlobalHotkey();
        }

        private void dataGroupDetailForm_Deactivate(object sender, EventArgs e)
        {
            unregisterGlobalHotkey();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (saveData())
            {
                switch (originModuleID)
                {
                    case globalConstants.NEW_GROUP_USER:
                        gutil.saveUserChangeLog(globalConstants.MENU_MANAJEMEN_USER, globalConstants.CHANGE_LOG_INSERT, "ADD NEW GROUP USER [" + namaGroupTextBox.Text + "]");
                        break;
                    case globalConstants.EDIT_GROUP_USER:
                        if (nonAktifCheckbox.Checked)
                            gutil.saveUserChangeLog(globalConstants.MENU_MANAJEMEN_USER, globalConstants.CHANGE_LOG_UPDATE, "UPDATE GROUP USER [" + namaGroupTextBox.Text + "] GROUP STATUS NON-AKTIF");
                        else
                            gutil.saveUserChangeLog(globalConstants.MENU_MANAJEMEN_USER, globalConstants.CHANGE_LOG_UPDATE, "UPDATE GROUP USER [" + namaGroupTextBox.Text + "] GROUP STATUS AKTIF");
                        break;
                }

                if (originModuleID != globalConstants.PENGATURAN_GRUP_AKSES)
                {
                    gutil.showSuccess(options);
                    gutil.ResetAllControls(this);

                    originModuleID = globalConstants.NEW_GROUP_USER;
                    options = gutil.INS;
                }
                else
                {
                    gutil.showSuccess(options);
                    this.Close();
                }
                //MessageBox.Show("SUCCESS");
                //this.Close();
            }
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            gutil.ResetAllControls(this);
        }
    }
}
