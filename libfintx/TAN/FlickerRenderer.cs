﻿/*	
 * 	
 *  This file is part of libfintx.
 *  
 *  Copyright (c) 2016 - 2017 Torsten Klinger
 * 	E-Mail: torsten.klinger@googlemail.com
 * 	
 * 	libfintx is free software; you can redistribute it and/or
 *	modify it under the terms of the GNU Lesser General Public
 * 	License as published by the Free Software Foundation; either
 * 	version 2.1 of the License, or (at your option) any later version.
 *	
 * 	libfintx is distributed in the hope that it will be useful,
 * 	but WITHOUT ANY WARRANTY; without even the implied warranty of
 * 	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * 	Lesser General Public License for more details.
 *	
 * 	You should have received a copy of the GNU Lesser General Public
 * 	License along with libfintx; if not, write to the Free Software
 * 	Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA
 * 	
 */

/*
 *
 *	Based on Olaf Willuhn's Java implementation of flicker code rendering,
 *	available at https://github.com/willuhn/hbci4java/blob/master/src/org/kapott/hbci/manager/FlickerRenderer.java
 *
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace libfintx
{
    public class FlickerRenderer
    {
        /// <summary>
        /// Default clock frequency in Hz
        /// </summary>
        public const int FREQUENCY_DEFAULT = 10;

        /// <summary>
        /// Minimal clock frequency
        /// </summary>
        public const int FREQUENCY_MIN = 2;

        /// <summary>
        /// Maximal clock frequency
        /// </summary>
        public const int FREQUENCY_MAX = 40;

        private int halfbyteid = 0;
        private int clock = 0;
        private IList<int[]> bitarray = null;

        private Thread thread = null;
        private int iterations = 0;
        private int freq = FREQUENCY_DEFAULT;

        /// <summary>
        /// Flicker graphic
        /// </summary>
        PictureBox pictureBox;

        public FlickerRenderer(string code, PictureBox pictureBox)
        {
            this.pictureBox = pictureBox;

            // Sync-ID
            code = "0FFF" + code;

            // Bitfield with BCD-Codierung
            IDictionary<string, int[]> bcdmap = new Dictionary<string, int[]>();
            bcdmap["0"] = new int[] { 0, 0, 0, 0, 0 };
            bcdmap["1"] = new int[] { 0, 1, 0, 0, 0 };
            bcdmap["2"] = new int[] { 0, 0, 1, 0, 0 };
            bcdmap["3"] = new int[] { 0, 1, 1, 0, 0 };
            bcdmap["4"] = new int[] { 0, 0, 0, 1, 0 };
            bcdmap["5"] = new int[] { 0, 1, 0, 1, 0 };
            bcdmap["6"] = new int[] { 0, 0, 1, 1, 0 };
            bcdmap["7"] = new int[] { 0, 1, 1, 1, 0 };
            bcdmap["8"] = new int[] { 0, 0, 0, 0, 1 };
            bcdmap["9"] = new int[] { 0, 1, 0, 0, 1 };
            bcdmap["A"] = new int[] { 0, 0, 1, 0, 1 };
            bcdmap["B"] = new int[] { 0, 1, 1, 0, 1 };
            bcdmap["C"] = new int[] { 0, 0, 0, 1, 1 };
            bcdmap["D"] = new int[] { 0, 1, 0, 1, 1 };
            bcdmap["E"] = new int[] { 0, 0, 1, 1, 1 };
            bcdmap["F"] = new int[] { 0, 1, 1, 1, 1 };

            // Swap left and right char of each byte
            this.bitarray = new List<int[]>();
            for (int i = 0; i < code.Length; i += 2)
            {
                bitarray.Add(bcdmap[Convert.ToString(code[i + 1])]);
                bitarray.Add(bcdmap[Convert.ToString(code[i])]);
            }
        }

        /// <summary>
        /// Sets the clock frequency in Hz
        /// </summary>
        public virtual int Frequency
        {
            set
            {
                if (value < FREQUENCY_MIN || value > FREQUENCY_MAX)
                {
                    return;
                }
                this.freq = value;
            }
        }

        /// <summary>
        /// Starts the rendering of the flicker code
        /// </summary>
        public void Start()
        {
            lock (this)
            {
                // Stop running threads
                Stop();

                this.thread = new Thread(Run);
                thread.Start();
            }
        }

        public virtual void Run()
        {
            // First semi byte
            this.halfbyteid = 0;

            // Clock
            this.clock = 1;

			
            // Flicker code form
            var pictureBox = this.pictureBox;

            // Flicker graphic
            Graphics graphic = Graphics.FromImage(pictureBox.Image);

            // Change between black and white
            Brush brush;
			
            try
            {
                // Transmission
                while (true)
                {
                    int[] bits = this.bitarray[this.halfbyteid];

                    bits[0] = this.clock;
					
					int margin = 7;
					int barwidth = pictureBox.Width / 5;

                    for (int i = 0; i < 5; i++)
                    {
                        if (bitarray[halfbyteid][i] == 1)
                        {
                            brush = Brushes.White;
                        }
                        else
                        {
                            brush = Brushes.Black;
                        }

                        graphic.FillRectangle(brush, i * barwidth + margin, margin, barwidth - 2 * margin, pictureBox.Height - 2 * margin);

                        // Refresh flicker code
                        pictureBox.Refresh();
                    }

                    this.clock--;
                    
					if (this.clock < 0)
                    {
                        this.clock = 1;

                        // Each character must be duplicated
                        // Once with clock 0 and once with clock 1
                        this.halfbyteid++;
                        if (this.halfbyteid >= this.bitarray.Count)
                        {
                            this.halfbyteid = 0;

                            // Flicker code was shown one period
                            this.iterations++;
                        }
                    }

                    // Waiting period
                    long sleep = 1000L / this.freq;
                    Thread.Sleep(Convert.ToInt32(sleep));
                }
            }
            catch
            {
                // End of display flicker code
            }
        }

        /// <summary>
        /// Stop rendering
        /// </summary>
        public void Stop()
        {
            if (this.thread != null)
            {
                try
                {
                    if (this.thread != null)
                    {
                        this.thread.Interrupt();
                        lock (this.thread)
                        {
                            Monitor.PulseAll(this.thread);
                        }
                    }
                }
                finally
                {
                    this.thread = null;
                }
            }
        }
    }
}
