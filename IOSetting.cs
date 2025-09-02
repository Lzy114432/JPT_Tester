using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SRTConfig;

namespace SRTLogic
{
    //IO设定，基本上是一定要调用这个的，不然映射和常开常闭的设定会有问题导致闪退
    public partial class IOSetting : Form
    {
        IO_Interface io;
        string inputcsvpath = "Config/InputSetting.csv";
        string outputcsvpath = "Config/OutputSetting.csv";
        List<IOProperties>[] map = new List<IOProperties>[2];
        int inputCount = 0;
        int outputCount = 0;
        public IOSetting(IO_Interface iopoint)
        {
            InitializeComponent();
            this.MaximumSize = this.Size;
            this.MinimumSize = this.Size;
            io = iopoint;
            inputCount = io.getInputCount();
            outputCount = io.getOutputCount();

            LoadCSV();

            setUI();
        }

        private void setUI()
        {
            for (int i = 0; i < map[0].Count; i++)
            {
                IOProperties da = map[0][i];
                DataGridViewRow row = new DataGridViewRow();
                DataGridViewCell id = new DataGridViewTextBoxCell();
                DataGridViewCell name = new DataGridViewTextBoxCell();
                DataGridViewCell mapping = new DataGridViewTextBoxCell();
                DataGridViewComboBoxCell state = new DataGridViewComboBoxCell();
                id.Value = i;
                name.Value = da.name;
                mapping.Value = da.map;
                state.Items.Add("常开");
                state.Items.Add("常闭");
                if (da.iostate == 1)
                {
                    state.Value = "常闭";
                }
                else
                {
                    state.Value = "常开";
                }

                row.Cells.Add(id);
                row.Cells.Add(name);
                row.Cells.Add(mapping);
                row.Cells.Add(state);
                inputGridView.Rows.Add(row);
            }
            for (int i = 0; i < map[1].Count; i++)
            {
                IOProperties da = map[1][i];
                DataGridViewRow row = new DataGridViewRow();

                DataGridViewCell id = new DataGridViewTextBoxCell();
                DataGridViewCell name = new DataGridViewTextBoxCell();
                DataGridViewCell mapping = new DataGridViewTextBoxCell();
                DataGridViewComboBoxCell state = new DataGridViewComboBoxCell();
                id.Value = i;
                name.Value = da.name;
                mapping.Value = da.map;
                state.Items.Add("常开");
                state.Items.Add("常闭");
                if (da.iostate == 1)
                {
                    state.Value = "常闭";
                }
                else
                {
                    state.Value = "常开";
                }

                row.Cells.Add(id);
                row.Cells.Add(name);
                row.Cells.Add(mapping);
                row.Cells.Add(state);
                outputGridView.Rows.Add(row);
            }
            inputGridView.Columns[0].ReadOnly = true;
            outputGridView.Columns[0].ReadOnly = true;
            inputGridView.Columns[1].ReadOnly = true;
            outputGridView.Columns[1].ReadOnly = true;

		}

		private bool DataToMap(bool isOutput, List<List<string>> readdata, out List<IOProperties> map)
        {
            map = new List<IOProperties>();
            if (isOutput)
            {
                for (int i = 0; i < outputCount; i++)
                {
                    if (i + 1 < readdata.Count)
                    {
                        List<string> linemap = readdata[i + 1];
                        if (linemap.Count == 4)
                        {
                            try
                            {
                                IOProperties lineda = new IOProperties();
                                lineda.name = linemap[1];
                                lineda.map = Convert.ToUInt32(linemap[2]);
                                lineda.iostate = Convert.ToUInt32(linemap[3]);
                                map.Add(lineda);
                            }
                            catch
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        IOProperties p = new IOProperties();
                        p.map = (uint)i;
                        p.name = "Y" + i;
                        map.Add(p);
                    }
                }
            }
            else
            {
                for (int i = 0; i < inputCount; i++)
                {
                    if (i + 1 < readdata.Count)
                    {
                        List<string> linemap = readdata[i + 1];
                        if (linemap.Count == 4)
                        {
                            try
                            {
                                IOProperties lineda = new IOProperties();
                                lineda.name = linemap[1];
                                lineda.map = Convert.ToUInt32(linemap[2]);
                                lineda.iostate = Convert.ToUInt32(linemap[3]);
                                map.Add(lineda);
                            }
                            catch
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        IOProperties p = new IOProperties();
                        p.map = (uint)i;
                        p.name = "X" + i;
                        map.Add(p);
                    }
                }
            }
            return true;
        }

        private void setDefault(bool isOutput,out List<IOProperties> map)
        {
            map = new List<IOProperties>();
            if (isOutput)
            {
                for (int i = 0; i < outputCount; i++)
                {
                    IOProperties p = new IOProperties();
                    p.map = (uint)i;
                    p.name = "Y" + i;
                    map.Add(p);
                }
            }
            else 
            {
                for (int i = 0; i < inputCount; i++)
                {
                    IOProperties p = new IOProperties();
                    p.map = (uint)i;
                    p.name = "X" + i;
                    map.Add(p);
                }
            }
            
        }

        public void SetIOMapping()
        {
            io.setInputMapping(map[0]);
            io.setOutputMapping(map[1]);
        }

        private void ok_button_Click(object sender, EventArgs e)
        {
            SaveMap();
            SaveCSV();
            SetIOMapping();
            Close();
        }

        private void SaveMap()
        {
            for (int i = 0; i < map[0].Count; i++)
            {
                IOProperties line = map[0][i];
                try
                {
                    line.name = inputGridView.Rows[i].Cells[1].Value.ToString();
                    line.map = Convert.ToUInt32(inputGridView.Rows[i].Cells[2].Value.ToString());
                    line.iostate = inputGridView.Rows[i].Cells[3].Value.ToString().Equals("常闭") ? (uint)1 : 0;
                    map[0][i] = line;
                }
                catch { }
            }
            for (int i = 0; i < map[1].Count; i++)
            {
                IOProperties line = map[1][i];
                try
                {
                    line.name = outputGridView.Rows[i].Cells[1].Value.ToString();
                    line.map = Convert.ToUInt32(outputGridView.Rows[i].Cells[2].Value.ToString());
                    line.iostate = outputGridView.Rows[i].Cells[3].Value.ToString().Equals("常闭") ? (uint)1 : 0;
                    map[1][i] = line;
                }
                catch { }
            }
        }

        private void LoadCSV()
        {
            List<List<string>> readdata;
            bool result = CSVFile.LoadFormCSV(inputcsvpath, out readdata);
            if (result)
            {
                bool res = DataToMap(false, readdata, out map[0]);
                if (!res)
                {
                    setDefault(false, out map[0]);
                }
            }
            else
            {
                setDefault(false, out map[0]);
            }

            result = CSVFile.LoadFormCSV(outputcsvpath, out readdata);
            if (result)
            {
                bool res = DataToMap(true, readdata, out map[1]);
                if (!res)
                {
                    setDefault(true, out map[1]);
                }
            }
            else
            {
                setDefault(true, out map[1]);
            }
        }
        private void SaveCSV()
        {
            List<List<string>> saveData = new List<List<string>>();
            List<string> head = new List<string>();
            head.Add("序号");
            head.Add("名称");
            head.Add("映射");
            head.Add("状态");
            saveData.Add(head);
            for (int i = 0; i < map[0].Count; i++)
            {
                IOProperties p = map[0][i];
                List<string> line = new List<string>();
                line.Add(i.ToString());
                line.Add(p.name);
                line.Add(p.map.ToString());
                line.Add(p.iostate.ToString());
                saveData.Add(line);
            }
            CSVFile.SaveToCSV(inputcsvpath, saveData);

            saveData.Clear();
            saveData.Add(head);
            for (int i = 0; i < map[1].Count; i++)
            {
                IOProperties p = map[1][i];
                List<string> line = new List<string>();
                line.Add(i.ToString());
                line.Add(p.name);
                line.Add(p.map.ToString());
                line.Add(p.iostate.ToString());
                saveData.Add(line);
            }
            CSVFile.SaveToCSV(outputcsvpath, saveData);
        }
    }
}
