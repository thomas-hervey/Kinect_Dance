using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    public partial class JSF : Form
    {
        // Varibale to check if form exited with safe
        public bool save = false;
        // Array to hold user's joint options
        public double[] jointTolerances = new double[15];
        // Array to hold effect options
        public String[] effectNames = new String[5];

        public JSF()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On load setup function
        /// </summary>
        /// <returns> N/A </returns>
        private void Form1_Load(object sender, EventArgs e)
        {
            // On load, disable all group controls
            foreach (Control t in jointSelectionGroup.Controls)
            {
                if (t is TextBox && t != null)
                {
                    t.Enabled = false;
                }
            }
            // On load, populate the effect drop down menu
            
        }

        /// <summary>
        /// Select all check boxes
        /// </summary>
        /// <returns> N/A </returns>
        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            foreach (Control c in jointSelectionGroup.Controls)
            {
                // If the group element is a check box, check it
                if (c is CheckBox && c != null)
                {
                    ((CheckBox)c).Checked = true;
                }
                // Fill textboxes with a 15 degree tolerance default
                if (c is TextBox && c != null)
                {
                    c.Text = "15";
                }

            }
        }

        /// <summary>
        /// Deselect all check boxes
        /// </summary>
        /// <returns> N/A </returns>
        private void btnDeselectAll_Click(object sender, EventArgs e)
        {
            foreach (Control c in jointSelectionGroup.Controls)
            {
                // If the group element is a check box, uncheck it
                if (c is CheckBox && c != null)
                {
                    ((CheckBox)c).Checked = false;
                }
                //  Clear all textboxes of any values
                if (c is TextBox && c != null)
                {
                    ((TextBox)c).Clear();
                }
            }

        }

        /// <summary>
        /// Update the MainWindow's jointTolerance array with user specified values
        /// </summary>
        /// <returns> N/A </returns>
        private void savePose_Click(object sender, EventArgs e)
        {

            // Default for unimportant joints
            int TOLERANCE_TERMINAL = -77;
            
            // Group item counter
            int i = 14;
            
            // Fill MainWindow's array of jointTolerances with the checked options
            foreach (Control t in jointSelectionGroup.Controls)
            {
                if (t is TextBox && t != null)
                {
                    if (((TextBox)t).Enabled == true)
                    {
                        this.jointTolerances[i] = Convert.ToDouble(t.Text);
                    }
                    else
                    {
                        this.jointTolerances[i] = TOLERANCE_TERMINAL;
                    }
                }
                i --;
            }

            save = true;
            this.Close();   
        }

        /// <summary>
        /// Cancel save dialog & close window
        /// </summary>
        /// <returns> N/A </returns>
        private void cancelSave_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Functions to enable/disable text boxes based on their corresponding check box
        /// </summary>
        /// <returns> N/A </returns>
        private void rightWristCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (rightWristCheck.Checked)
            {
                rightWristText.Enabled = true;
            }
            else
            {
                rightWristText.Enabled = false;
                rightWristText.Clear();
            }
        }
        private void rightElbowCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (rightElbowCheck.Checked)
            {
                rightElbowText.Enabled = true;
            }
            else
            {
                rightElbowText.Enabled = false;
                rightElbowText.Clear();
            }
        }
        private void rightShoulderCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (rightShoulderCheck.Checked)
            {
                rightShoulderText.Enabled = true;
            }
            else
            {
                rightShoulderText.Enabled = false;
                rightShoulderText.Clear();
            }

        }
        private void leftWristCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (leftWristCheck.Checked)
            {
                leftWristText.Enabled = true;
            }
            else
            {
                leftWristText.Enabled = false;
                leftWristText.Clear();
            }
        }
        private void leftElbowCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (leftElbowCheck.Checked)
            {
                leftElbowText.Enabled = true;
            }
            else
            {
                leftElbowText.Enabled = false;
                leftElbowText.Clear();
            }
        }
        private void leftShoulderCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (leftShoulderCheck.Checked)
            {
                leftShoulderText.Enabled = true;
            }
            else
            {
                leftShoulderText.Enabled = false;
                leftShoulderText.Clear();
            }
        }
        private void rightAnkleCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (rightAnkleCheck.Checked)
            {
                rightAnkleText.Enabled = true;
            }
            else
            {
                rightAnkleText.Enabled = false;
                rightAnkleText.Clear();
            }
        }
        private void rightKneeCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (rightKneeCheck.Checked)
            {
                rightKneeText.Enabled = true;
            }
            else
            {
                rightKneeText.Enabled = false;
                rightKneeText.Clear();
            }
        }
        private void rightHipCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (rightHipCheck.Checked)
            {
                rightHipText.Enabled = true;
            }
            else
            {
                rightHipText.Enabled = false;
                rightHipText.Clear();
            }
        }
        private void leftAnkleCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (leftAnkleCheck.Checked)
            {
                leftAnkleText.Enabled = true;
            }
            else
            {
                leftAnkleText.Enabled = false;
                leftAnkleText.Clear();
            }
        }
        private void leftKneeCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (leftKneeCheck.Checked)
            {
                leftKneeText.Enabled = true;
            }
            else
            {
                leftKneeText.Enabled = false;
                leftKneeText.Clear();
            }
        }
        private void leftHipCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (leftHipCheck.Checked)
            {
                leftHipText.Enabled = true;
            }
            else
            {
                leftHipText.Enabled = false;
                leftHipText.Clear();
            }
        }
        private void spineCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (spineCheck.Checked)
            {
                spineText.Enabled = true;
            }
            else
            {
                spineText.Enabled = false;
                spineText.Clear();
            }
        }
        private void neckCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (neckCheck.Checked)
            {
                neckText.Enabled = true;
            }
            else
            {
                neckText.Enabled = false;
                neckText.Clear();
            }
        }
        private void centerShouldersCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (centerShouldersCheck.Checked)
            {
                centerShouldersText.Enabled = true;
            }
            else
            {
                centerShouldersText.Enabled = false;
                centerShouldersText.Clear();
            }
        }

        /// <summary>
        /// Handler for the effect selection drop down menu
        /// </summary>
        /// <returns> N/A </returns>
        private void effectSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

    }
}
