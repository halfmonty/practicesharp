﻿#region © Copyright 2010 Yuval Naveh, Practice Sharp. LGPL.
/* Practice Sharp
 
    © Copyright 2010, Yuval Naveh.
     All rights reserved.
 
    This file is part of Practice Sharp.

    Practice Sharp is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Practice Sharp is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser Public License for more details.

    You should have received a copy of the GNU Lesser Public License
    along with Practice Sharp.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using BigMansStuff.PracticeSharp.Core;
using System.IO;
using System.Threading;
using System.Xml;
using System.Configuration;

namespace BigMansStuff.PracticeSharp.UI
{
    public partial class MainForm : Form
    {
        #region Construction

        /// <summary>
        /// Constructor
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }
      
        /// <summary>
        /// Form loading event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                InitializeApplication();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed initialize the Practice Sharp back end - " + ex.ToString());
            }

            AutoLoadLastFile();
        }

        /// <summary>
        /// Initialize the PracticeSharp Application
        /// </summary>
        private void InitializeApplication()
        {
            InitializeConfiguration();
            InitializeMRUFiles();

            // Create the PracticeSharpLogic back end layer
            m_practiceSharpLogic = new PracticeSharpLogic();
            m_practiceSharpLogic.Initialize();
            m_practiceSharpLogic.StatusChanged += new PracticeSharpLogic.StatusChangedEventHandler(practiceSharpLogic_StatusChanged);
            m_practiceSharpLogic.PlayTimeChanged += new EventHandler(practiceSharpLogic_PlayTimeChanged);
            m_practiceSharpLogic.CueWaitPulsed += new EventHandler(practiceSharpLogic_CueWaitPulsed);

            EnableControls( false );

            openFileDialog.InitialDirectory = Properties.Settings.Default.LastAudioFolder;

            playPauseButton.Image = Resources.Play_Normal;
            writeBankButton.Image = Resources.save_icon;
            resetBankButton.Image = Resources.Eraser_icon;
            openFileButton.Image = Resources.OpenFile_icon;

            cueComboBox.SelectedIndex = 0;
            m_presetControls = new Dictionary<string, PresetControl>();
            m_presetControls.Add("1", presetControl1);
            m_presetControls.Add("2", presetControl2);
            m_presetControls.Add("3", presetControl3);
            m_presetControls.Add("4", presetControl4);


            // Set defaults
            tempoTrackBar_ValueChanged(this, new EventArgs());
            volumeTrackBar_ValueChanged(this, new EventArgs());
            playTimeTrackBar_ValueChanged(this, new EventArgs());

            // presetControl1.State = PresetControl.PresetStates.Selected;
        }

        private void InitializeMRUFiles()
        {
            m_recentFilesMenuItems.AddRange(new ToolStripMenuItem[] { 
                        recent1ToolStripMenuItem,  recent2ToolStripMenuItem, recent3ToolStripMenuItem, recent4ToolStripMenuItem, recent5ToolStripMenuItem,
                        recent6ToolStripMenuItem, recent7ToolStripMenuItem, recent8ToolStripMenuItem });
            foreach (ToolStripMenuItem recentMenuItem in m_recentFilesMenuItems)
            {
                recentMenuItem.Click += new EventHandler(recentMenuItem_Click);
            }

            m_mruFile = m_appDataFolder + "\\practicesharp_mru.txt";
            m_mruManager = new MRUManager(m_recentFilesMenuItems.Count, m_mruFile);
        }


        private void InitializeConfiguration()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            Console.WriteLine("Local user config path: {0}", config.FilePath);

            // Get current application version
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            m_appVersion = assembly.GetName().Version;
            string appVersionString = m_appVersion.ToString();

            // Upgrade user settings from last version to current version, if needed
            string appVersionSetting = Properties.Settings.Default.ApplicationVersion;
            if (appVersionSetting != m_appVersion.ToString())
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.ApplicationVersion = appVersionString;
            }
            // Show current application version
            this.Text = string.Format(Resources.AppTitle, m_appVersion.ToString());
            // Initialize Application Date Folder - used for storing Preset Bank files
            m_appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\PracticeSharp";
            if (!Directory.Exists(m_appDataFolder))
            {
                Directory.CreateDirectory(m_appDataFolder);
            }
        }


        #endregion

        // TODO: Create/Find a hover button control that switches images from Regular image to hot image
        // TODO: SoundTouch Release DLL crashes. Check why Debug works but Release does not.

        #region Destruction

        /// <summary>
        /// Form closing (before it actually closes) event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_practiceSharpLogic != null)
            {
                m_practiceSharpLogic.Dispose();
            }
        }

        #endregion

        #region GUI Event Handlers
        /// <summary>
        /// Play/Pause button click event handler - Plays or Pauses the current play back of the file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playPauseButton_Click(object sender, EventArgs e)
        {
            if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Playing)
            {
                playPauseButton.Image = Resources.Play_Hot;
                m_practiceSharpLogic.Pause();
            }
            else if ( (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Pausing ) ||
                      (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Initialized ) )
            {
                playPauseButton.Image = Resources.Pause_Hot;

                m_practiceSharpLogic.Play();
            }
            else if ( m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Stopped )
            {
                // Playing has stopped, need to reload the file
                if (playTimeTrackBar.Value == playTimeTrackBar.Maximum)
                    playTimeTrackBar.Value = playTimeTrackBar.Minimum;
                m_practiceSharpLogic.LoadFile( m_currentFilename );

                playPauseButton.Image = Resources.Pause_Hot;
                m_practiceSharpLogic.Play();
            }
        }

        /// <summary>
        /// MouseEnter event handler - Handles hover start logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playPauseButton_MouseEnter(object sender, EventArgs e)
        {
            if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Playing)
                playPauseButton.Image = Resources.Pause_Hot;
            else if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Pausing ||
                     m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Stopped)
                playPauseButton.Image = Resources.Play_Hot;
        }

        /// <summary>
        /// MouseLeave event handler - Handles hover begin logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playPauseButton_MouseLeave(object sender, EventArgs e)
        {
            if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Playing)
                playPauseButton.Image = Resources.Pause_Normal;
            else if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Pausing || 
                     m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Stopped)
                playPauseButton.Image = Resources.Play_Normal;

        }

        /// <summary>
        /// Paints the PositionMarkersPanel - Paints the Start marker, End marker and region in-between
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void positionMarkersPanel_Paint(object sender, PaintEventArgs e)
        {
            if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Initialized)
            {
                return;
            }

            TimeSpan startMarker = m_practiceSharpLogic.StartMarker;
            TimeSpan endMarker = m_practiceSharpLogic.EndMarker;
            TimeSpan filePlayDuration = m_practiceSharpLogic.FilePlayDuration;

            int startMarkerX;
            int endMarkerX;
            if (filePlayDuration.TotalSeconds <= 0)
            {
                startMarkerX = 0;
                endMarkerX = 0;
            }
            else
            {
                startMarkerX = Convert.ToInt32(startMarker.TotalSeconds / filePlayDuration.TotalSeconds * positionMarkersPanel.Width);
                endMarkerX = Convert.ToInt32(endMarker.TotalSeconds / filePlayDuration.TotalSeconds * positionMarkersPanel.Width);
            }

            // Draw the whole loop region - Start marker to End Marker
            e.Graphics.FillRectangle(Brushes.Wheat, startMarkerX, 0, 
                                                             endMarkerX - startMarkerX, MarkerHeight);
            // Draw just the start marker
            e.Graphics.FillRectangle(Brushes.LightGreen, startMarkerX, 0, MarkerWidth, MarkerHeight);
            // Draw just the end marker
            e.Graphics.FillRectangle(Brushes.LightBlue, endMarkerX - MarkerWidth, 0, MarkerWidth, MarkerHeight);
        }

        /// <summary>
        /// Click event handler for Write Preset Button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void writePresetButton_Click(object sender, EventArgs e)
        {
            if (m_currentPreset == null ||
                m_currentPreset.State != PresetControl.PresetStates.WaitForSave)
            {
                // Temporary Pause play until save has completed
                if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Playing)
                {
                    m_tempSavePausePlay = true;
                    m_practiceSharpLogic.Pause();
                }

                // Enter preset 'Write Mode'
                foreach (PresetControl presetControl in m_presetControls.Values)
                {
                    presetControl.State = PresetControl.PresetStates.WaitForSave;
                }
            }
            else if ( m_currentPreset != null )
            {
                // Cancel write mode
                m_currentPreset.State = PresetControl.PresetStates.Selected;
                if (m_tempSavePausePlay)
                {
                    m_practiceSharpLogic.Play();
                    m_tempSavePausePlay = false;
                }
            }
        }

        /// <summary>
        /// PresetSelected Event handler - Handles a preset select request (user clicking on preset requesting to activate it)
        /// As a result the preset values are applied
        /// Note: The preset can be selected even if it is active - in this case the values will revert to the last saved values
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void presetControl_PresetSelected(object sender, EventArgs e)
        {
            m_currentPreset = sender as PresetControl;
            foreach (PresetControl presetControl in m_presetControls.Values)
            {
                if (presetControl != m_currentPreset)
                {
                    presetControl.State = PresetControl.PresetStates.Off;
                }
            }

            PresetData presetData = m_currentPreset.PresetData;

            ApplyPresetValueUIControls(presetData);
        }

        
        /// <summary>
        /// PresetDescriptionChanged Event handler - When a preset description changed the preset bank has to be rewritten to persist the change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void presetControl_PresetDescriptionChanged(object sender, EventArgs e)
        {
            // (Re-)Write preset bank file
            WritePresetsBank();
        }

        /// <summary>
        /// PresetSaveSelected Event handler - Handles a preset save request (user clicks on preset when Write Mode is on, i.e. Red Leds are turned on)
        /// As a result the preset is saved into the preset bank file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void presetControl_PresetSaveSelected(object sender, EventArgs e)
        {
            m_currentPreset = sender as PresetControl;

            foreach (PresetControl presetControl in m_presetControls.Values)
            {
                if (presetControl != m_currentPreset)
                {
                    presetControl.State = PresetControl.PresetStates.Off;
                }
                else
                {
                    if (presetControl.PresetData.Description == string.Empty)
                    {
                        presetControl.ChangeDescription();
                    }

                    // Update the preset data for the selected preset
                    presetControl.PresetData.Tempo = m_practiceSharpLogic.Tempo;
                    presetControl.PresetData.Pitch = m_practiceSharpLogic.Pitch;
                    presetControl.PresetData.Volume = m_practiceSharpLogic.Volume;
                    presetControl.PresetData.CurrentPlayTime = m_practiceSharpLogic.CurrentPlayTime;
                    presetControl.PresetData.StartMarker = m_practiceSharpLogic.StartMarker;
                    presetControl.PresetData.EndMarker = m_practiceSharpLogic.EndMarker;
                    presetControl.PresetData.Cue = m_practiceSharpLogic.Cue;
                    presetControl.PresetData.Loop = m_practiceSharpLogic.Loop;
                    presetControl.PresetData.Description = presetControl.PresetDescription;
                }
            }

            // (Re-)Write preset bank file
            WritePresetsBank();

            if (m_tempSavePausePlay)
            {
                m_practiceSharpLogic.Play();
                m_tempSavePausePlay = false;
            }
        }

        private void volumeTrackBar_ValueChanged(object sender, EventArgs e)
        {
            float newVolume = volumeTrackBar.Value / 100.0f;
            m_practiceSharpLogic.Volume = newVolume;

            volumeValueLabel.Text = ( newVolume * 100 ).ToString();

        }
   
        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void resetBankButton_Click(object sender, EventArgs e)
        {

        }

        private void resetBankButton_MouseDown(object sender, MouseEventArgs e)
        {
            resetBankTimer.Start();
        }

        private void resetBankTimer_Tick(object sender, EventArgs e)
        {
            resetBankTimer.Stop();

            // Reset bank values
            m_currentPreset.Reset();
            ApplyPresetValueUIControls( m_currentPreset.PresetData );

            WritePresetsBank();
        }

        private void resetBankButton_MouseUp(object sender, MouseEventArgs e)
        {
            // Cancel reset
            resetBankTimer.Stop();
        }

        private void tempoTrackBar_MouseDown(object sender, MouseEventArgs e)
        {
            UpdateTrackBarByMousePosition(tempoTrackBar, e);
        }

        private void pitchTrackBar_MouseDown(object sender, MouseEventArgs e)
        {
            UpdateTrackBarByMousePosition(pitchTrackBar, e);
        }

        private void volumeTrackBar_MouseDown(object sender, MouseEventArgs e)
        {
            UpdateTrackBarByMousePosition(volumeTrackBar, e);
        }

        

        #region Drag & Drop

        /// <summary>
        /// Drag Enter - The inital action when the dragged file enters the form, but not released yet
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string droppedFile = files[0];
                Console.WriteLine("[DragEnter] DragAndDrop Files: " + droppedFile);
                if (droppedFile.ToLower().EndsWith(".mp3") || droppedFile.ToLower().EndsWith(".wav"))
                {
                    e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    Console.WriteLine("DragAndDrop are only allowed for music files (MP3, WAV)");
                    e.Effect = DragDropEffects.None;
                }
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// Drag Drop - Handles the final action when the file is dropped (released over the form)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);
                string droppedFile = files[0];
                Console.WriteLine("[DragDrop] DragAndDrop Files: " + droppedFile );
                OpenFile(droppedFile, true);
            }
        }

        #endregion
        private void currentUpDown_ValueChanged(object sender, EventArgs e)
        {
            // Don't allow re-entry of UI events when the track bar is being programmatically changed
            if (m_ignorePlayTimeUIEvents)
                return;

            // Mask out PracticeSharpLogic events to eliminate 'Racing' between GUI and PracticeSharpLogic over current playtime
            m_currentControlsMaskOutTime = DateTime.Now.AddMilliseconds(500);

            UpdateCoreCurrentPlayTime();
        }

        #region Start & End Loop Markers

        private void startLoopSecondUpDown_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan startMarker = m_practiceSharpLogic.StartMarker;

            if (startLoopSecondUpDown.Value < 0)
            {
                startMarker = startMarker.Subtract(new TimeSpan(0, 0, 1));
            }
            else if (startLoopSecondUpDown.Value > 59)
            {
                startMarker = startMarker.Add(new TimeSpan(0, 0, 1));
            }
            else
            {
                startMarker = new TimeSpan(0, 0, startMarker.Minutes, Convert.ToInt32(startLoopSecondUpDown.Value), startMarker.Milliseconds);
            }

            UpdateCoreStartMarker(startMarker);
        }

        private void startLoopMilliUpDown_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan startMarker = m_practiceSharpLogic.StartMarker;

            if (startLoopMilliUpDown.Value < 0)
            {
                startMarker = startMarker.Subtract(new TimeSpan(0, 0, 0, 0, 1));
            }
            else if (startLoopMilliUpDown.Value > 999)
            {
                startMarker = startMarker.Add(new TimeSpan(0, 0, 0, 0, 1));
            }
            else
            {
                startMarker = new TimeSpan(0, 0, startMarker.Minutes, startMarker.Seconds, Convert.ToInt32(startLoopMilliUpDown.Value));
            }

            UpdateCoreStartMarker(startMarker);
        }

        private void startLoopMinuteUpDown_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan startMarker = m_practiceSharpLogic.StartMarker;

            if (startLoopMinuteUpDown.Value < 0)
            {
                startMarker = startMarker.Subtract(new TimeSpan(0, 0, 1, 0, 0));
            }
            else if (startLoopMinuteUpDown.Value > 99)
            {
                startMarker = startMarker.Add(new TimeSpan(0, 0, 1, 0, 0));
            }
            else
            {
                startMarker = new TimeSpan(0, 0, Convert.ToInt32(startLoopMinuteUpDown.Value), startMarker.Seconds, startMarker.Milliseconds);
            }

            UpdateCoreStartMarker(startMarker);
        }

        private void endLoopSecondUpDown_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan endMarker = m_practiceSharpLogic.EndMarker;

            if (endLoopSecondUpDown.Value < 0)
            {
                endMarker = endMarker.Subtract(new TimeSpan(0, 0, 1));
            }
            else if (endLoopSecondUpDown.Value > 59)
            {
                endMarker = endMarker.Add(new TimeSpan(0, 0, 1));
            }
            else
            {
                endMarker = new TimeSpan(0, 0, endMarker.Minutes, Convert.ToInt32(endLoopSecondUpDown.Value), endMarker.Milliseconds);
            }

            UpdateCoreEndMarker(endMarker);
        }

        private void endLoopMinuteUpDown_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan endMarker = m_practiceSharpLogic.EndMarker;

            if (endLoopMinuteUpDown.Value < 0)
            {
                endMarker = endMarker.Subtract(new TimeSpan(0, 0, 1, 0, 0));
            }
            else if (endLoopMinuteUpDown.Value > 99)
            {
                endMarker = endMarker.Add(new TimeSpan(0, 0, 1, 0, 0));
            }
            else
            {
                endMarker = new TimeSpan(0, 0, Convert.ToInt32(endLoopMinuteUpDown.Value), endMarker.Seconds, endMarker.Milliseconds);
            }

            UpdateCoreEndMarker(endMarker);
        }

        private void endLoopMilliUpDown_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan endMarker = m_practiceSharpLogic.EndMarker;

            if (endLoopMilliUpDown.Value < 0)
            {
                endMarker = endMarker.Subtract(new TimeSpan(0, 0, 0, 0, 1));
            }
            else if (endLoopMilliUpDown.Value > 999)
            {
                endMarker = endMarker.Add(new TimeSpan(0, 0, 0, 0, 1));
            }
            else
            {
                endMarker = new TimeSpan(0, 0, endMarker.Minutes, endMarker.Seconds, Convert.ToInt32(endLoopMilliUpDown.Value));
            }

            UpdateCoreEndMarker(endMarker);
        }


        private void startLoopNowButton_Click(object sender, EventArgs e)
        {
            // Handle special case when Now is clicked after the End marker
            if (m_practiceSharpLogic.CurrentPlayTime > m_practiceSharpLogic.EndMarker)
            {
                endLoopMinuteUpDown.Value = currentMinuteUpDown.Value;
                endLoopSecondUpDown.Value = currentSecondUpDown.Value;
                endLoopMilliUpDown.Value = currentMilliUpDown.Value;
            }

            startLoopMinuteUpDown.Value = currentMinuteUpDown.Value;
            startLoopSecondUpDown.Value = currentSecondUpDown.Value;
            startLoopMilliUpDown.Value = currentMilliUpDown.Value;
        }

        private void endLoopNowButton_Click(object sender, EventArgs e)
        {
            endLoopMinuteUpDown.Value = currentMinuteUpDown.Value;
            endLoopSecondUpDown.Value = currentSecondUpDown.Value;
            endLoopMilliUpDown.Value = currentMilliUpDown.Value;
        }


        #endregion

        /// <summary>
        /// Click event handler for openFileButton - Invokes the open file dialog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openFileButton_Click(object sender, EventArgs e)
        {
            // Show the open file dialog
            if (DialogResult.OK == openFileDialog.ShowDialog(this))
            {
                // Get directory path and store it as a user settting
                FileInfo fi = new FileInfo( openFileDialog.FileName );
                Properties.Settings.Default.LastAudioFolder = fi.Directory.FullName;
                Properties.Settings.Default.Save();

                // Open the file for playing
                OpenFile(openFileDialog.FileName, true);
            }
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            m_practiceSharpLogic.Stop();
            playTimeUpdateTimer.Enabled = false;
        }

        /// <summary>
        /// Event handler for ValueChanged of the tempoTrackBar -
        ///   Changes the underlying tempo of PracticeSharpLogic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tempoTrackBar_ValueChanged(object sender, EventArgs e)
        {
            // Convert to Percent 
            float newTempo = tempoTrackBar.Value / 100.0f;
            // Assign new Tempo
            m_practiceSharpLogic.Tempo = newTempo;
            
            // Update speed value label
            speedValueLabel.Text = newTempo.ToString();
        }

        private void pitchTrackBar_ValueChanged(object sender, EventArgs e)
        {
            // Convert to Percent 
            float newPitch = pitchTrackBar.Value / 96.0f;
            // Assign new Pitch
            m_practiceSharpLogic.Pitch = newPitch;

            // Update Pitch value label
            pitchValueLabel.Text = newPitch.ToString( "0.00" );
    
        } 

        private void speedLabel_Click(object sender, EventArgs e)
        {
            tempoTrackBar.Value = Convert.ToInt32( PresetData.DefaultTempo * 100 );
        }

        private void volumeLabel_Click(object sender, EventArgs e)
        {
            volumeTrackBar.Value = Convert.ToInt32( PresetData.DefaultVolume * 100 );
        }

        private void pitchLabel_Click(object sender, EventArgs e)
        {
              pitchTrackBar.Value = Convert.ToInt32( PresetData.DefaultPitch * 100 );
        }

        private void positionLabel_Click(object sender, EventArgs e)
        {
            // Reset current play time so it starts from the begining
            if (m_practiceSharpLogic.Loop)
            {
                // In case of a loop, move the current play time to the start marker
                m_practiceSharpLogic.CurrentPlayTime = m_practiceSharpLogic.StartMarker;
            }
            else
            {
                m_practiceSharpLogic.CurrentPlayTime = TimeSpan.Zero;
            }
        }
    
        private void loopCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            m_practiceSharpLogic.Loop = loopCheckBox.Checked;
        }

        private void playTimeTrackBar_ValueChanged(object sender, EventArgs e)
        {
            // Don't allow re-entry of UI events when the track bar is being programmtically changed
            if (m_ignorePlayTimeUIEvents)
                return;

            float playPosSeconds = ( float ) ( playTimeTrackBar.Value / 100.0f * m_practiceSharpLogic.FilePlayDuration.TotalSeconds );
            TimeSpan newPlayTime = new TimeSpan( 0, 0, 0, ( int ) playPosSeconds, 
                    ( int ) ( 100 * ( playPosSeconds - Math.Truncate( playPosSeconds ) ) )) ;

            m_practiceSharpLogic.CurrentPlayTime = newPlayTime;
            if (m_practiceSharpLogic.Status != PracticeSharpLogic.Statuses.Playing)
            {
                UpdateCurrentUpDownControls(newPlayTime);
            }
        }

        private void playPositionTrackBar_MouseDown(object sender, MouseEventArgs e)
        {
            m_isUpdatePlayTimeNeeded = false;
            m_playTimeTrackBarIsChanging = true;

            Application.DoEvents();
            // Let previous pending updates to position track bar to complete - otherwise the track bar 'jumps'
            Thread.Sleep(100);
            Application.DoEvents();

            UpdateNewPlayTimeByMousePos(e);
        }

        private void playTimeTrackBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (m_playTimeTrackBarIsChanging)
            {
                UpdateNewPlayTimeByMousePos(e);
            }
        }

        private void playPositionTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            m_playTimeTrackBarIsChanging = false;
        }

        private void UpdateNewPlayTimeByMousePos(MouseEventArgs e)
        {
            const int TrackBarMargin = 10;
            float duration = (float)m_practiceSharpLogic.FilePlayDuration.TotalSeconds;
            float newValue = duration * (((float)e.X - TrackBarMargin) / (playTimeTrackBar.Width - TrackBarMargin * 2 ));
            if (newValue > duration)
                newValue = duration;
            else if (newValue < 0)
                newValue = 0;

            TimeSpan newPlayTime = new TimeSpan(0, 0, Convert.ToInt32( newValue ) );
            m_practiceSharpLogic.CurrentPlayTime = newPlayTime;

            int newTrackBarValue = Convert.ToInt32(newValue / duration * 100.0f);

            if (m_practiceSharpLogic.Status == PracticeSharpLogic.Statuses.Playing)
            {
                m_ignorePlayTimeUIEvents = true;
                try
                {
                    // Time in which playtime trackbar updates coming from PracticeSharpLogic are not allowed (to eliminate 'Jumps' due to old locations)
                    m_playTimeTrackBarMaskOutTime = DateTime.Now.AddMilliseconds(500);

                    playTimeTrackBar.Value = newTrackBarValue;
                }
                finally
                {
                    m_ignorePlayTimeUIEvents = false;
                }
            }
            else
            {
                playTimeTrackBar.Value = newTrackBarValue;
            }
        }

        private void playTimeUpdateTimer_Tick(object sender, EventArgs e)
        {
            lock (this)
            {
                if (!m_isUpdatePlayTimeNeeded)
                    return;
            }

            // Only update when file is playing
            if (m_practiceSharpLogic.Status != PracticeSharpLogic.Statuses.Playing)
                return;

            m_isUpdatePlayTimeNeeded = false;

            m_ignorePlayTimeUIEvents = true;
            try
            {
                if (DateTime.Now > m_currentControlsMaskOutTime)
                {
                    UpdateCurrentUpDownControls(m_practiceSharpLogic.CurrentPlayTime);
                }

                if (!m_playTimeTrackBarIsChanging && DateTime.Now > m_playTimeTrackBarMaskOutTime)
                {
                    int currentPlayTimeValue = Convert.ToInt32(100.0f * m_practiceSharpLogic.CurrentPlayTime.TotalSeconds / m_practiceSharpLogic.FilePlayDuration.TotalSeconds);
                    playTimeTrackBar.Value = currentPlayTimeValue;
                }

                positionMarkersPanel.Refresh();
            }
            finally
            {
                m_ignorePlayTimeUIEvents = false;
            }
        }

        private void UpdateCurrentUpDownControls( TimeSpan playTime )
        {
            // Update current play time controls
            currentMinuteUpDown.Value = playTime.Minutes;
            currentSecondUpDown.Value = playTime.Seconds;
            currentMilliUpDown.Value = playTime.Milliseconds;
        }

        private void cueComboBox_SelectedValueChanged(object sender, EventArgs e)
        {
            m_practiceSharpLogic.Cue = new TimeSpan(0, 0, Convert.ToInt32(cueComboBox.Text));
        }

        #region Recent Files (MRU)
        private void recentFilesToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            List<string> mruItems = m_mruManager.Items;

            for (int menuItemIndex = 0; menuItemIndex < m_recentFilesMenuItems.Count; menuItemIndex++)
            {
                ToolStripMenuItem menuItem = m_recentFilesMenuItems[menuItemIndex];

                menuItem.Visible = menuItemIndex < mruItems.Count;
                if (menuItemIndex < mruItems.Count)
                {
                    string recentFilename = mruItems[menuItemIndex];
                    menuItem.Visible = true;
                    int rightIndex = 0;
                    string recentFilenameDisplay = string.Empty;
                    if (recentFilename.Length > MaxRecentDisplayLength)
                    {
                        rightIndex = recentFilename.Length - MaxRecentDisplayLength;
                        recentFilenameDisplay = recentFilename.Substring(0, 3) + " ... " + recentFilename.Substring(rightIndex, MaxRecentDisplayLength);
                    }
                    else
                    {
                        recentFilenameDisplay = recentFilename;
                    }

                    menuItem.Text = recentFilenameDisplay;
                    menuItem.Tag = recentFilename;

                    // Disable current file in recent MRU items - its already open
                    if (recentFilename == m_currentFilename)
                    {
                        menuItem.Enabled = false;
                    }
                    else
                    {
                        menuItem.Enabled = true;
                    }
                }
                else
                {
                    menuItem.Visible = false;
                }

            }
        }

        private void recentMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            string selectedRecentFilename = (string)menuItem.Tag;

            if (!OpenFile(selectedRecentFilename, true))
            {
                m_mruManager.Remove(selectedRecentFilename);
            }
        }

        #endregion
        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutForm aboutForm = new AboutForm())
            {
                aboutForm.AppVersion = m_appVersion;
                aboutForm.ShowDialog(this);
            }

        }

        #endregion

        #region PracticeSharpLogic Event Handlers

        /// <summary>
        /// Event handler for PracticeSharpLogic status changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="newStatus"></param>
        private void practiceSharpLogic_StatusChanged(object sender, PracticeSharpLogic.Statuses newStatus)
        {
            this.BeginInvoke( new MethodInvoker( delegate()
            {
                playStatusToolStripLabel.Text = newStatus.ToString();

                if ( (newStatus == PracticeSharpLogic.Statuses.Stopped)
                   || (newStatus == PracticeSharpLogic.Statuses.Pausing)
                   || (newStatus == PracticeSharpLogic.Statuses.Error) )
                {
                    playPauseButton.Image = Resources.Play_Normal;
                    playTimeUpdateTimer.Enabled = false;
                }
                else if (newStatus == PracticeSharpLogic.Statuses.Playing)
                {
                    playPauseButton.Image = Resources.Pause_Normal;
                    playTimeUpdateTimer.Enabled = true;
                }
            } )
            );
        }

        private void practiceSharpLogic_PlayTimeChanged(object sender, EventArgs e)
        {
            this.Invoke(
                new MethodInvoker(delegate()
                {
                    lock (this)
                    {
                        m_isUpdatePlayTimeNeeded = true;
                    }
                }));
        }

        private void practiceSharpLogic_CueWaitPulsed(object sender, EventArgs e)
        {
            this.BeginInvoke(

                new MethodInvoker(delegate()
                {
                    // Pulse the cue led once
                    cuePictureBox.Image = Resources.blue_on_16;
                    Application.DoEvents();

                    Thread.Sleep(100);
                    cuePictureBox.Image = Resources.blue_off_16;

                    Application.DoEvents();
                }
             ));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Automatically loads the last played file (and its presets bank if it exists)
        /// </summary>
        private void AutoLoadLastFile()
        {
            string lastFilename = Properties.Settings.Default.LastFilename;

            if (File.Exists(lastFilename))
            {
                // Open file but don't start playing automatically
                OpenFile(lastFilename, false);
            }
        }

        /// <summary>
        /// Open the given file
        /// </summary>
        /// <param name="filename"></param>
        private bool OpenFile(string filename, bool autoPlay)
        {
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                if (!File.Exists(filename))
                {
                    MessageBox.Show("File does not exist: " + filename, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                m_mruManager.Add(filename);
                playTimeUpdateTimer.Enabled = false;

                Properties.Settings.Default.LastFilename = filename;
                Properties.Settings.Default.Save();

                m_currentFilename = filename;
                filenameToolStripStatusLabel.Text = filename;
                m_practiceSharpLogic.LoadFile(filename);

                // Load Presets Bank for this input file
                LoadPresetsBank();

                EnableControls(true);

                playDurationLabel.Text =
                       string.Format("{0}:{1}", m_practiceSharpLogic.FilePlayDuration.Minutes.ToString("00"),
                                    m_practiceSharpLogic.FilePlayDuration.Seconds.ToString("00"));
                play1QDurationLabel.Text =
                       string.Format("{0}:{1}", (m_practiceSharpLogic.FilePlayDuration.TotalSeconds / 4 / 60).ToString("00"),
                                    (m_practiceSharpLogic.FilePlayDuration.Seconds / 4).ToString("00"));
                play2QDurationLabel.Text =
                       string.Format("{0}:{1}", (m_practiceSharpLogic.FilePlayDuration.Minutes / 2).ToString("00"),
                                    (m_practiceSharpLogic.FilePlayDuration.Seconds / 2).ToString("00"));
                play3QDurationLabel.Text =
                       string.Format("{0}:{1}", (m_practiceSharpLogic.FilePlayDuration.TotalSeconds * 3 / 4 / 60).ToString("00"),
                                    (m_practiceSharpLogic.FilePlayDuration.Seconds * 3 / 4).ToString("00"));

                if (autoPlay)
                {
                    playPauseButton.Image = Resources.Pause_Normal;
                    m_practiceSharpLogic.Play();
                }
            }
            finally
            {
                playTimeUpdateTimer.Enabled = true;
                Cursor.Current = Cursors.Default;
            }

            return true;
        }

        /// <summary>
        /// Utility function - Enables/Disabled UI controls
        /// </summary>
        /// <param name="enabled"></param>
        private void EnableControls(bool enabled)
        {
            tempoTrackBar.Enabled = enabled;
            pitchTrackBar.Enabled = enabled;
            volumeTrackBar.Enabled = enabled;
            playTimeTrackBar.Enabled = enabled;
            playPauseButton.Enabled = enabled;
            startLoopNowButton.Enabled = enabled;
            endLoopNowButton.Enabled = enabled;
            cueComboBox.Enabled = enabled;
            writeBankButton.Enabled = enabled;
            resetBankButton.Enabled = enabled;
            presetControl1.Enabled = enabled;
            presetControl2.Enabled = enabled;
            presetControl3.Enabled = enabled;
            presetControl4.Enabled = enabled;
            loopPanel.Enabled = enabled;
            currentMinuteUpDown.Enabled = enabled;
            currentSecondUpDown.Enabled = enabled;
            currentMilliUpDown.Enabled = enabled;
        }

        private void UpdateCoreCurrentPlayTime()
        {
            m_practiceSharpLogic.CurrentPlayTime = new TimeSpan(0, 0, (int)currentMinuteUpDown.Value, (int)currentSecondUpDown.Value, (int)currentMilliUpDown.Value);
        }

        private void UpdateCoreStartMarker(TimeSpan startMarker)
        {
            if (startMarker > m_practiceSharpLogic.EndMarker)
            {
                startMarker = m_practiceSharpLogic.EndMarker;
            }
            else if (startMarker < TimeSpan.Zero)
            {
                startMarker = TimeSpan.Zero;
            }

            m_practiceSharpLogic.StartMarker = startMarker;

            positionMarkersPanel.Refresh();

            ApplyLoopStartMarkerUI(startMarker);
        }

        private void ApplyLoopStartMarkerUI(TimeSpan startMarker)
        {
            startLoopMinuteUpDown.ValueChanged -= startLoopMinuteUpDown_ValueChanged;
            startLoopSecondUpDown.ValueChanged -= startLoopSecondUpDown_ValueChanged;
            startLoopMilliUpDown.ValueChanged -= startLoopMilliUpDown_ValueChanged;
            try
            {
                startLoopMinuteUpDown.Value = startMarker.Minutes;
                startLoopSecondUpDown.Value = startMarker.Seconds;
                startLoopMilliUpDown.Value = startMarker.Milliseconds;
            }
            finally
            {
                startLoopMinuteUpDown.ValueChanged += startLoopMinuteUpDown_ValueChanged;
                startLoopSecondUpDown.ValueChanged += startLoopSecondUpDown_ValueChanged;
                startLoopMilliUpDown.ValueChanged += startLoopMilliUpDown_ValueChanged;
            }
        }

        private void ApplyLoopEndMarkerUI(TimeSpan endMarker)
        {
            endLoopMinuteUpDown.ValueChanged -= endLoopMinuteUpDown_ValueChanged;
            endLoopSecondUpDown.ValueChanged -= endLoopSecondUpDown_ValueChanged;
            endLoopMilliUpDown.ValueChanged -= endLoopMilliUpDown_ValueChanged;
            try
            {
                endLoopMinuteUpDown.Value = endMarker.Minutes;
                endLoopSecondUpDown.Value = endMarker.Seconds;
                endLoopMilliUpDown.Value = endMarker.Milliseconds;
            }
            finally
            {
                endLoopMinuteUpDown.ValueChanged += endLoopMinuteUpDown_ValueChanged;
                endLoopSecondUpDown.ValueChanged += endLoopSecondUpDown_ValueChanged;
                endLoopMilliUpDown.ValueChanged += endLoopMilliUpDown_ValueChanged;
            }
        }

        private void UpdateCoreEndMarker(TimeSpan endMarker)
        {
            if (endMarker < m_practiceSharpLogic.StartMarker)
            {
                endMarker = m_practiceSharpLogic.StartMarker;
            }
            else if (endMarker > m_practiceSharpLogic.FilePlayDuration)
            {
                endMarker = m_practiceSharpLogic.FilePlayDuration;
            }

            m_practiceSharpLogic.EndMarker = endMarker;

            positionMarkersPanel.Refresh();

            ApplyLoopEndMarkerUI(endMarker);
        }

        #region Presets

        /// <summary>
        /// Loads the presets from the preset bank file
        /// </summary>
        private void LoadPresetsBank()
        {
            m_presetsBankFilename = m_appDataFolder + "\\" + Path.GetFileName(m_currentFilename) + ".practicesharpbank.xml";

            if (!File.Exists(m_presetsBankFilename))
            {
                return;
            }

            try
            {
                // Loads the presets bank XML file
                XmlDocument doc = new XmlDocument();
                doc.Load(m_presetsBankFilename);

                XmlElement root = doc.DocumentElement;
                XmlNode presetsBankNode = root.SelectSingleNode("/" + XML_Node_Root + "/" + XML_Node_PresetsBank);
                string activePresetId = presetsBankNode.Attributes[XML_Attr_ActivePreset].Value;
                XmlNodeList presetNodes = presetsBankNode.SelectNodes(XML_Node_Preset);
                // Load all preset nodes
                foreach (XmlNode presetNode in presetNodes)
                {
                    string presetId = presetNode.Attributes[XML_Attr_Id].Value;
                    // Load XML values into PresetData object
                    PresetData presetData = m_presetControls[presetId].PresetData;
                    presetData.Tempo = Convert.ToSingle(presetNode.Attributes[XML_Attr_Tempo].Value);
                    presetData.Pitch = Convert.ToSingle(presetNode.Attributes[XML_Attr_Pitch].Value);
                    presetData.Volume = Convert.ToSingle(presetNode.Attributes[XML_Attr_Volume].Value);

                    presetData.CurrentPlayTime = TimeSpan.Parse(presetNode.Attributes[XML_Attr_PlayTime].Value);
                    presetData.StartMarker = TimeSpan.Parse(presetNode.Attributes[XML_Attr_LoopStartMarker].Value);
                    presetData.EndMarker = TimeSpan.Parse(presetNode.Attributes[XML_Attr_LoopEndMarker].Value);
                    presetData.Loop = Convert.ToBoolean(presetNode.Attributes[XML_Attr_IsLoop].Value);
                    presetData.Cue = TimeSpan.Parse(presetNode.Attributes[XML_Attr_Cue].Value);
                    presetData.Description = Convert.ToString(presetNode.Attributes[XML_Attr_Description].Value);

                    PresetControl presetControl = m_presetControls[presetId];
                    presetControl.PresetDescription = presetData.Description;
                }

                m_currentPreset = m_presetControls[activePresetId];
                m_currentPreset.State = PresetControl.PresetStates.Selected;
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Failed loading Presets Bank for file: " + m_currentFilename, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }

        }

        /// <summary>
        /// Writes a full Preset Bank XML into a file
        /// </summary>
        private void WritePresetsBank()
        {
            // Create an XML Document
            XmlDocument doc = new XmlDocument();
            XmlElement elRoot = (XmlElement)doc.AppendChild(doc.CreateElement(XML_Node_Root));
            elRoot.SetAttribute(XML_Attr_Version, m_appVersion.ToString());
            XmlElement elPresets = (XmlElement)elRoot.AppendChild(doc.CreateElement(XML_Node_PresetsBank));
            elPresets.SetAttribute(XML_Attr_Filename, Path.GetFileName(m_currentFilename));
            elPresets.SetAttribute(XML_Attr_ActivePreset, m_currentPreset.Id);

            // Write XML Nodes for each Preset
            foreach (PresetControl presetControl in m_presetControls.Values)
            {
                PresetData presetData = presetControl.PresetData;

                XmlElement elPreset = (XmlElement)elPresets.AppendChild(doc.CreateElement(XML_Node_Preset));
                // Write Preset Attributes
                elPreset.SetAttribute(XML_Attr_Id, presetControl.Id);
                elPreset.SetAttribute(XML_Attr_Tempo, presetData.Tempo.ToString());
                elPreset.SetAttribute(XML_Attr_Pitch, presetData.Pitch.ToString());
                elPreset.SetAttribute(XML_Attr_Volume, presetData.Volume.ToString());
                elPreset.SetAttribute(XML_Attr_PlayTime, presetData.CurrentPlayTime.ToString());
                elPreset.SetAttribute(XML_Attr_LoopStartMarker, presetData.StartMarker.ToString());
                elPreset.SetAttribute(XML_Attr_LoopEndMarker, presetData.EndMarker.ToString());
                elPreset.SetAttribute(XML_Attr_IsLoop, presetData.Loop.ToString());
                elPreset.SetAttribute(XML_Attr_Cue, presetData.Cue.ToString());
                elPreset.SetAttribute(XML_Attr_IsLoop, presetData.Loop.ToString());
                elPreset.SetAttribute(XML_Attr_Description, presetData.Description);
            }

            // Write to XML file
            using (StreamWriter writer = new StreamWriter(m_presetsBankFilename, false, Encoding.UTF8))
            {
                writer.Write(doc.OuterXml);
            }
        }

        /// <summary>
        /// Applies the preset values to UI controls - Effectively loads the UI controls with preset values
        /// </summary>
        /// <param name="presetData"></param>
        private void ApplyPresetValueUIControls(PresetData presetData)
        {
            // Apply preset values
            tempoTrackBar.Value = Convert.ToInt32(presetData.Tempo * 100.0f);
            pitchTrackBar.Value = Convert.ToInt32(presetData.Pitch * 96.0f);
            volumeTrackBar.Value = Convert.ToInt32(presetData.Volume * 100.0f);
            if (m_practiceSharpLogic.FilePlayDuration == TimeSpan.Zero)
            {
                playTimeTrackBar.Value = playTimeTrackBar.Minimum;
            }
            else
            {
                playTimeTrackBar.Value = Convert.ToInt32(100.0f * presetData.CurrentPlayTime.TotalSeconds / m_practiceSharpLogic.FilePlayDuration.TotalSeconds);
            }

            ApplyLoopStartMarkerUI(presetData.StartMarker);
            ApplyLoopEndMarkerUI(presetData.EndMarker);

            m_practiceSharpLogic.StartMarker = presetData.StartMarker;
            m_practiceSharpLogic.EndMarker = presetData.EndMarker;

            int cueItemIndex = cueComboBox.FindString(Convert.ToInt32(presetData.Cue.TotalSeconds).ToString());
            cueComboBox.SelectedIndex = cueItemIndex;
            loopCheckBox.Checked = presetData.Loop;
            positionMarkersPanel.Refresh();
        }

        #endregion
        private void UpdateTrackBarByMousePosition(TrackBar trackBar, MouseEventArgs e)
        {
            const int TrackBarMargin = 10;
            float maxValue = trackBar.Maximum;
            float minValue = trackBar.Minimum;
            float newValue = minValue + (maxValue - minValue) * (((float)e.X - TrackBarMargin) / (trackBar.Width - TrackBarMargin * 2));
            if (newValue > maxValue)
                newValue = maxValue;
            else if (newValue < minValue)
                newValue = minValue;

            int newTrackBarValue = Convert.ToInt32(newValue);

            trackBar.Value = newTrackBarValue;
        }

        #endregion 

        #region Private Members

        private bool m_isUpdatePlayTimeNeeded;
        private PracticeSharpLogic m_practiceSharpLogic;
        
        /// <summary>
        /// PresetControls dictionary:
        /// Key = Id
        /// Value = PresetControl Instance
        /// </summary>
        private Dictionary<string, PresetControl> m_presetControls;
        private PresetControl m_currentPreset;

        private bool m_ignorePlayTimeUIEvents = false;
        private bool m_playTimeTrackBarIsChanging = false;

        /// <summary>
        /// Flag for Temporary Pausing Play while saving
        /// </summary>
        private bool m_tempSavePausePlay = false;

        private DateTime m_playTimeTrackBarMaskOutTime = DateTime.Now;
        private DateTime m_currentControlsMaskOutTime = DateTime.Now;

        private string m_currentFilename;
        private string m_presetsBankFilename;
        private string m_appDataFolder;
        private Version m_appVersion;

        private MRUManager m_mruManager;
        private string m_mruFile;
        private List<ToolStripMenuItem> m_recentFilesMenuItems = new List<ToolStripMenuItem>();

        #endregion

        #region Constants

        const int MarkerWidth = 5; 
        const int MarkerHeight = 10;

        const int MaxRecentDisplayLength = 60;

        #endregion
   
        #region XML Constants
        const string XML_Node_Root = "PracticeSharp";
        const string XML_Node_PresetsBank = "PresetsBank";
        const string XML_Node_Preset = "Preset";
        const string XML_Attr_ActivePreset = "ActivePreset";
        const string XML_Attr_Version = "Version";
        const string XML_Attr_Filename = "Filename";
        const string XML_Attr_Id = "Id";
        const string XML_Attr_Tempo = "Tempo";
        const string XML_Attr_Pitch = "Pitch";
        const string XML_Attr_Volume = "Volume";         
        const string XML_Attr_PlayTime = "PlayTime";
        const string XML_Attr_LoopStartMarker = "LoopStartMarker";
        const string XML_Attr_LoopEndMarker = "LoopEndMarker";
        const string XML_Attr_IsLoop = "IsLoop";
        const string XML_Attr_Cue = "Cue";
        const string XML_Attr_Description = "Description";

        #endregion

    }
}