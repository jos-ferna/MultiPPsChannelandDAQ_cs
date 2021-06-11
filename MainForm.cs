//=============================================================================================================
//
// Title:
//      NI-DCPower Single Point Multi-Channel Synchronization
//
// Description:
//      Demonstrates how to use triggers and events to synchronize multiple channels in
//      Single Point source mode.
//      
//      Note: In this example the Output Function is set to DC Voltage. If you change the 
//      Output Function to DC Current, you must use Current Level and Voltage Limit instead
//      of Voltage Level and Current Limit.
//
//=============================================================================================================

using System;
using System.Windows.Forms;
using NationalInstruments.ModularInstruments.NIDCPower;
using NationalInstruments.ModularInstruments.SystemServices.DeviceServices;

namespace NationalInstruments.Examples.SinglePointMultiChannelSync
{
    public partial class MainForm : Form
    {
        const int NumberOfSlaveDevices = 2;

        NIDCPower masterSession;
        NIDCPower[] slaveSession = new NIDCPower[NumberOfSlaveDevices];

        public MainForm()
        {
            InitializeComponent();
            ConfigureSlavesConfigurationDataGridView();
            ConfigureSlavesMeasurementDataGridView();
        }

        #region MainForm initial configuration
        void ConfigureSlavesConfigurationDataGridView()
        {
            LoadDCPowerDeviceNames();
            for (int i = 0; i < NumberOfSlaveDevices; i++)
            {
                slavesConfigurationDataGridView.Rows.Add(
                    String.Format("Slave {0}", i),
                    masterConfigurationResourceNameComboBox.Text,
                    "0",
                    2.ToString("E"),
                    (0.02).ToString("E"));
            }
            slavesConfigurationDataGridView.CellEnter += new DataGridViewCellEventHandler(AllowSingleClickEditInDataGridView);
        }

        void AllowSingleClickEditInDataGridView(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView target = sender as DataGridView;
            if (target != null)
            {
                target.BeginEdit(true);
                ComboBox cmb = target.EditingControl as ComboBox;
                if (cmb != null)
                {
                    cmb.DroppedDown = true;
                }
            }
        }

        void ConfigureSlavesMeasurementDataGridView()
        {
            for (int i = 0; i < NumberOfSlaveDevices; i++)
            {
                slavesMeasurementsDataGridView.Rows.Add(
                    String.Format("Slave {0}", i + 1),
                    0.ToString("E"),
                    0.ToString("E"));
            }
        }

        void LoadDCPowerDeviceNames()
        {
            using (ModularInstrumentsSystem dcPowerDevices = new ModularInstrumentsSystem("NI-DCPower"))
            {
                foreach (DeviceInfo device in dcPowerDevices.DeviceCollection)
                {
                    masterConfigurationResourceNameComboBox.Items.Add(device.Name);
                    ((DataGridViewComboBoxColumn)(slavesConfigurationDataGridView.Columns[1])).Items.Add(device.Name);
                }
            }
            if (masterConfigurationResourceNameComboBox.Items.Count > 0)
            {
                masterConfigurationResourceNameComboBox.SelectedIndex = 0;
            }
        }
        #endregion

        #region MainForm configuration values
        string MasterResourceName
        {
            get
            {
                return this.masterConfigurationResourceNameComboBox.Text;
            }
        }

        string MasterChannelName
        {
            get
            {
                return this.masterConfigurationChannelNameTextBox.Text;
            }
        }

        double MasterCurrentLimit
        {
            get
            {
                return decimal.ToDouble(this.masterConfigurationCurrentLimitNumeric.Value);
            }
        }

        double MasterVoltageLevel
        {
            get
            {
                return decimal.ToDouble(this.masterConfigurationVoltageLevelNumeric.Value);
            }
        }

        PrecisionTimeSpan MasterSourceDelay
        {
            get
            {
                return new PrecisionTimeSpan(decimal.ToDouble(this.masterConfigurationSourceDelayNumeric.Value));
            }
        }

        class SlaveConfiguration
        {
            int _index;
            MainForm _thisForm;

            internal SlaveConfiguration(MainForm form, int index)
            {
                _index = index;
                _thisForm = form;
            }

            internal string ResourceName
            {
                get
                {
                    return _thisForm.slavesConfigurationDataGridView.Rows[_index].Cells[1].Value.ToString();
                }
            }

            internal string ChannelName
            {
                get
                {
                    return _thisForm.slavesConfigurationDataGridView.Rows[_index].Cells[2].Value.ToString();
                }
            }

            internal double VoltageLevel
            {
                get
                {
                    return Double.Parse(_thisForm.slavesConfigurationDataGridView.Rows[_index].Cells[3].Value.ToString());
                }
            }

            internal double CurrentLimit
            {
                get
                {
                    return Double.Parse(_thisForm.slavesConfigurationDataGridView.Rows[_index].Cells[4].Value.ToString());
                }
            }
        }

        SlaveConfiguration[] _slave;
        SlaveConfiguration[] Slave
        {
            get
            {
                if (_slave == null)
                {
                    _slave = new SlaveConfiguration[NumberOfSlaveDevices];
                    for (int i = 0; i < NumberOfSlaveDevices; i++)
                    {
                        _slave[i] = new SlaveConfiguration(this, i);
                    }
                }
                return _slave;
            }
        }
        #endregion

        void startButton_Click(object sender, System.EventArgs e)
        {
            ChangeControlState(false);
            Start();
            ChangeControlState(true);
        }

        void mainForm_Closing(object sender, FormClosingEventArgs e)
        {
            CloseSession();
        }

        void Start()
        {
            try
            {
                InitializeDCPowerSessions();

                #region Configure the master device
                masterSession.Source.Mode = DCPowerSourceMode.SinglePoint;
                masterSession.Outputs[MasterChannelName].Source.Output.Function = DCPowerSourceOutputFunction.DCVoltage;
                masterSession.Outputs[MasterChannelName].Source.Voltage.VoltageLevel = MasterVoltageLevel;
                masterSession.Outputs[MasterChannelName].Source.Voltage.CurrentLimit = MasterCurrentLimit;
                masterSession.Outputs[MasterChannelName].Source.SourceDelay = MasterSourceDelay;

                // Configure the Source trigger. On the master device, the Source trigger is
                // disabled (None). This means that the device will source without waiting for
                // a trigger.  When the device starts sourcing, it will export the Source trigger.
                masterSession.Triggers.SourceTrigger.Disable();

                // Configure when to take measurements.  On the master device, take a measurement
                // as soon as the source unit completes (including any source delay).
                masterSession.Measurement.Configuration.MeasureWhen = DCPowerMeasurementWhen.AutomaticallyAfterSourceComplete;

                masterSession.Control.Commit();
                #endregion

                #region Configure the slave devices
                string sourceTriggerInputTerminal = BuildTerminalName(masterSession, MasterChannelName, "SourceTrigger");
                string measureTriggerInputTerminal = BuildTerminalName(masterSession, MasterChannelName, "SourceCompleteEvent");

                for (int i = 0; i < NumberOfSlaveDevices; i++)
                {
                    slaveSession[i].Source.Mode = DCPowerSourceMode.SinglePoint;
                    slaveSession[i].Outputs[Slave[i].ChannelName].Source.Output.Function = DCPowerSourceOutputFunction.DCVoltage;
                    slaveSession[i].Outputs[Slave[i].ChannelName].Source.Voltage.VoltageLevel = Slave[i].VoltageLevel;
                    slaveSession[i].Outputs[Slave[i].ChannelName].Source.Voltage.CurrentLimit = Slave[i].CurrentLimit;

                    // Configure the Source Delay. On the slave device(s), set the delay to 30uS, so that the slave(s)
                    // are ready to receive the next trigger from the master as quickly as possible.
                    slaveSession[i].Outputs[Slave[i].ChannelName].Source.SourceDelay = new PrecisionTimeSpan(0.00003);

                    // Configure the Source trigger.  On the slave device(s), the source trigger is the exported 
                    // Source trigger from the master device.
                    slaveSession[i].Triggers.SourceTrigger.DigitalEdge.Configure(sourceTriggerInputTerminal, DCPowerTriggerEdge.Rising);

                    // Configure when to take measurements. On the slave device(s), take a measurement when the
                    // source unit on the master device completes. This is accomplished by setting Measure When
                    // to On Measure Trigger and by setting the input terminal of the Measure trigger to be the
                    // master device's Source Complete event.
                    slaveSession[i].Measurement.Configuration.MeasureWhen = DCPowerMeasurementWhen.AutomaticallyAfterSourceComplete;
                    slaveSession[i].Triggers.MeasureTrigger.DigitalEdge.Configure(measureTriggerInputTerminal, DCPowerTriggerEdge.Rising);

                    slaveSession[i].Control.Commit();
                }
                #endregion

                // Initiate the slave device(s) and then initiate the master device to start generation
                // and acquisition. The order here is significant.

                // Initiate the slave device(s). Once the for loop completes, the slave device(s) will
                // be waiting for the Source Trigger.
                for (int i = 0; i < NumberOfSlaveDevices; i++)
                {
                    slaveSession[i].Control.Initiate();
                }

                // Initiate the master device. The master device has to be initiated after the slave device(s)
                // so that when it exports the Source trigger the slave device(s) are already waiting for
                // the Source trigger.
                masterSession.Control.Initiate();

                // Fetch measurements from master device and update Mainform with the new values.
                DCPowerFetchResult result = masterSession.Measurement.Fetch(MasterChannelName, new PrecisionTimeSpan(1.0), 1);

                masterMeasurementsVoltageTextBox.Text = result.VoltageMeasurements[0].ToString("E");
                masterMeasurementsCurrentTextBox.Text = result.CurrentMeasurements[0].ToString("E");

                // Fetch measurements from slave device(s) and update Mainform with the new values.
                for (int i = 0; i < NumberOfSlaveDevices; i++)
                {
                    result = slaveSession[i].Measurement.Fetch(Slave[i].ChannelName, new PrecisionTimeSpan(10.0), 1);
                    slavesMeasurementsDataGridView.Rows[i].Cells[1].Value = result.VoltageMeasurements[0].ToString("E");
                    slavesMeasurementsDataGridView.Rows[i].Cells[2].Value = result.CurrentMeasurements[0].ToString("E");
                }

                masterSession.Utility.Reset();
                for (int i = 0; i < NumberOfSlaveDevices; i++)
                {
                    slaveSession[i].Utility.Reset();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                CloseSession();
            }
        }

        void InitializeDCPowerSessions()
        {
            masterSession = new NIDCPower(MasterResourceName, MasterChannelName, false);
            masterSession.DriverOperation.Warning += new EventHandler<DCPowerWarningEventArgs>(DCPowerDriverOperationWarning);

            for (int i = 0; i < NumberOfSlaveDevices; i++)
            {
                slaveSession[i] = new NIDCPower(Slave[i].ResourceName, Slave[i].ChannelName, false);
                slaveSession[i].DriverOperation.Warning += new EventHandler<DCPowerWarningEventArgs>(DCPowerDriverOperationWarning);
            }
        }

        void DCPowerDriverOperationWarning(object sender, DCPowerWarningEventArgs e)
        {
            MessageBox.Show(e.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void CloseSession()
        {
            try
            {
                if (masterSession != null)
                {
                    masterSession.Close();
                    masterSession = null;
                }
                if (slaveSession != null)
                {
                    for (int i = 0; i < NumberOfSlaveDevices; i++)
                    {
                        if (slaveSession[i] != null)
                        {
                            slaveSession[i].Close();
                            slaveSession[i] = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
                Application.Exit();
            }
        }

        static void ShowError(Exception ex)
        {
            MessageBox.Show(ex.Message, ex.GetType().ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static string BuildTerminalName(NIDCPower session, string channelName, string localTerminalName)
        {
            string modelName = session.Identity.InstrumentModel;
            string ioResourceDescriptor = session.DriverOperation.IOResourceDescriptor;
            if (modelName.Equals("NI PXI-4132", StringComparison.OrdinalIgnoreCase))
            {
                return String.Format("/{0}/{1}", ioResourceDescriptor, localTerminalName);
            }
            else
            {
                return String.Format("/{0}/Engine{1}/{2}", ioResourceDescriptor, channelName, localTerminalName);
            }
        }

        void ChangeControlState(bool flag)
        {
            masterConfigurationGroupBox.Enabled = flag;
            slavesConfigurationGroupBox.Enabled = flag;
            startButton.Enabled = flag;
            masterConfigurationResourceNameComboBox.Select();
            this.Refresh();
        }

    }
}