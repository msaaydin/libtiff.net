﻿/* Copyright (C) 2008-2009, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * This software is based in part on the work of the Sam Leffler, Silicon 
 * Graphics, Inc. and contributors.
 *
 * Copyright (c) 1988-1997 Sam Leffler
 * Copyright (c) 1991-1997 Silicon Graphics, Inc.
 * For conditions of distribution and use, see the accompanying README file.
 */

/*
 * Predictor Tag Support (used by multiple codecs).
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace BitMiracle.LibTiff.Internal
{
    /// <summary>
    /// Codecs that want to support the Predictor tag should inherit from 
    /// this class instead of TiffCodec. 
    /// 
    /// Such codecs should not override default TiffCodec's methods for 
    /// decode|encode setup and encoding|decoding of row|tile|strip. 
    /// Codecs with predictor support should override equivalent methods 
    /// provided by this class.
    /// 
    /// If codec wants to provide custom tag get|set|print methods, then
    /// it should pass pointer to a object derived from TiffTagMethods
    /// as parameter to TIFFPredictorInit
    /// </summary>
    class CodecWithPredictor : TiffCodec
    {
        public const int FIELD_PREDICTOR = (FIELD.FIELD_CODEC + 0);
        
        private enum PredictorType
        {
            ptNone,
            ptHorAcc8,
            ptHorAcc16,
            ptHorAcc32,
            ptSwabHorAcc16,
            ptSwabHorAcc32,
            ptHorDiff8,
            ptHorDiff16,
            ptHorDiff32,
            ptFpAcc,
            ptFpDiff,
        };

        private static TiffFieldInfo[] predictFieldInfo = 
        {
            new TiffFieldInfo(TIFFTAG.TIFFTAG_PREDICTOR, 1, 1, TiffDataType.TIFF_SHORT, CodecWithPredictor.FIELD_PREDICTOR, false, false, "Predictor"), 
        };

        private PREDICTOR predictor; /* predictor tag value */
        private int stride; /* sample stride over data */
        private int rowsize; /* tile/strip row size */

        private TiffTagMethods m_parentTagMethods;
        private TiffTagMethods m_tagMethods;
        private TiffTagMethods m_childTagMethods; // could be null

        private bool m_passThruDecode;
        private bool m_passThruEncode;

        private PredictorType m_predictorType; /* horizontal differencer/accumulator */

        public CodecWithPredictor(Tiff tif, COMPRESSION scheme, string name)
            : base(tif, scheme, name)
        {
            m_tagMethods = new CodecWithPredictorTagMethods();
        }

        // tagMethods can be null
        public void TIFFPredictorInit(TiffTagMethods tagMethods)
        {
            /*
            * Merge codec-specific tag information and
            * override parent get/set field methods.
            */
            m_tif.MergeFieldInfo(predictFieldInfo, predictFieldInfo.Length);
            m_childTagMethods = tagMethods;
            m_parentTagMethods = m_tif.m_tagmethods;
            m_tif.m_tagmethods = m_tagMethods;

            predictor = PREDICTOR.PREDICTOR_NONE; /* default value */
            m_predictorType = PredictorType.ptNone; /* no predictor routine */
        }

        public void TIFFPredictorCleanup()
        {
            m_tif.m_tagmethods = m_parentTagMethods;
        }

        //////////////////////////////////////////////////////////////////////////
        // WARNING: do not override this methods!
        //          please override their equivalents listed below

        public override bool tif_setupdecode()
        {
            return PredictorSetupDecode();
        }

        public override bool tif_decoderow(byte[] pp, int cc, ushort s)
        {
            if (!m_passThruDecode)
                return PredictorDecodeRow(pp, cc, s);

            return predictor_decoderow(pp, cc, s);
        }

        public override bool tif_decodestrip(byte[] pp, int cc, ushort s)
        {
            if (!m_passThruDecode)
                return PredictorDecodeTile(pp, cc, s);

            return predictor_decodestrip(pp, cc, s);
        }

        public override bool tif_decodetile(byte[] pp, int cc, ushort s)
        {
            if (!m_passThruDecode)
                return PredictorDecodeTile(pp, cc, s);

            return predictor_decodetile(pp, cc, s);
        }

        public override bool tif_setupencode()
        {
            return PredictorSetupEncode();
        }

        public override bool tif_encoderow(byte[] pp, int cc, ushort s)
        {
            if (!m_passThruEncode)
                return PredictorEncodeRow(pp, cc, s);

            return predictor_encoderow(pp, cc, s);
        }

        public override bool tif_encodestrip(byte[] pp, int cc, ushort s)
        {
            if (!m_passThruEncode)
                return PredictorEncodeTile(pp, cc, s);

            return predictor_encodestrip(pp, cc, s);
        }

        public override bool tif_encodetile(byte[] pp, int cc, ushort s)
        {
            if (!m_passThruEncode)
                return PredictorEncodeTile(pp, cc, s);

            return predictor_encodetile(pp, cc, s);
        }

        //////////////////////////////////////////////////////////////////////////
        // derived class should override methods below instead of 
        // TiffCodec's methods

        public virtual bool predictor_setupdecode()
        {
            return base.tif_setupdecode();
        }

        public virtual bool predictor_decoderow(byte[] pp, int cc, ushort s)
        {
            return base.tif_decoderow(pp, cc, s);
        }

        public virtual bool predictor_decodestrip(byte[] pp, int cc, ushort s)
        {
            return base.tif_decodestrip(pp, cc, s);
        }

        public virtual bool predictor_decodetile(byte[] pp, int cc, ushort s)
        {
            return base.tif_decodetile(pp, cc, s);
        }

        public virtual bool predictor_setupencode()
        {
            return base.tif_setupencode();
        }

        public virtual bool predictor_encoderow(byte[] pp, int cc, ushort s)
        {
            return base.tif_encoderow(pp, cc, s);
        }

        public virtual bool predictor_encodestrip(byte[] pp, int cc, ushort s)
        {
            return base.tif_encodestrip(pp, cc, s);
        }

        public virtual bool predictor_encodetile(byte[] pp, int cc, ushort s)
        {
            return base.tif_encodetile(pp, cc, s);
        }

        public PREDICTOR GetPredictorValue()
        {
            return predictor;
        }

        public void SetPredictorValue(PREDICTOR value)
        {
            predictor = value;
        }

        // retrieves child object's tag methods (could be null)
        public TiffTagMethods GetChildTagMethods()
        {
            return m_childTagMethods;
        }

        private void predictorFunc(byte[] cp0, int offset, int cc)
        {
            switch (m_predictorType)
            {
                case PredictorType.ptHorAcc8:
                    horAcc8(cp0, offset, cc);
                    break;
                case PredictorType.ptHorAcc16:
                    horAcc16(cp0, offset, cc);
                    break;
                case PredictorType.ptHorAcc32:
                    horAcc32(cp0, offset, cc);
                    break;
                case PredictorType.ptSwabHorAcc16:
                    swabHorAcc16(cp0, offset, cc);
                    break;
                case PredictorType.ptSwabHorAcc32:
                    swabHorAcc32(cp0, offset, cc);
                    break;
                case PredictorType.ptHorDiff8:
                    horDiff8(cp0, offset, cc);
                    break;
                case PredictorType.ptHorDiff16:
                    horDiff16(cp0, offset, cc);
                    break;
                case PredictorType.ptHorDiff32:
                    horDiff32(cp0, offset, cc);
                    break;
                case PredictorType.ptFpAcc:
                    fpAcc(cp0, offset, cc);
                    break;
                case PredictorType.ptFpDiff:
                    fpDiff(cp0, offset, cc);
                    break;
            }
        }

        private void horAcc8(byte[] cp0, int offset, int cc)
        {
            int cp = offset;
            if (cc > stride)
            {
                cc -= stride;
                /*
                * Pipeline the most common cases.
                */
                if (stride == 3)
                {
                    uint cr = cp0[cp];
                    uint cg = cp0[cp + 1];
                    uint cb = cp0[cp + 2];
                    do
                    {
                        cc -= 3;
                        cp += 3;

                        cr += cp0[cp];
                        cp0[cp] = (byte)cr;
                        
                        cg += cp0[cp + 1];
                        cp0[cp + 1] = (byte)cg;

                        cb += cp0[cp + 2];
                        cp0[cp + 2] = (byte)cb;
                    }
                    while (cc > 0);
                }
                else if (stride == 4)
                {
                    uint cr = cp0[cp];
                    uint cg = cp0[cp + 1];
                    uint cb = cp0[cp + 2];
                    uint ca = cp0[cp + 3];
                    do
                    {
                        cc -= 4;
                        cp += 4;

                        cr += cp0[cp];
                        cp0[cp] = (byte)cr;

                        cg += cp0[cp + 1];
                        cp0[cp + 1] = (byte)cg;

                        cb += cp0[cp + 2];
                        cp0[cp + 2] = (byte)cb;

                        ca += cp0[cp + 3];
                        cp0[cp + 3] = (byte)ca;
                    }
                    while (cc > 0);
                }
                else
                {
                    do
                    {
                        if (stride > 4 || stride < 0)
                        {
                            for (int i = stride - 4; i > 0; i--)
                            {
                                cp0[cp + stride] = (byte)(cp0[cp + stride] + cp0[cp]);
                                cp++;
                            }
                        }
                        else
                        {
                            for (int i = stride; i > 0; i--)
                            {
                                cp0[cp + stride] = (byte)(cp0[cp + stride] + cp0[cp]);
                                cp++;
                            }
                        }

                        cc -= stride;
                    }
                    while (cc > 0);
                }
            }
        }

        private void horAcc16(byte[] cp0, int offset, int cc)
        {
            ushort[] wp = Tiff.byteArrayToUInt16(cp0, offset, cc);
            int wpPos = 0;

            int wc = cc / 2;
            if (wc > stride)
            {
                wc -= stride;
                do
                {
                    if (stride > 4 || stride < 0)
                    {
                        for (int i = stride - 4; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }
                    else
                    {
                        for (int i = stride; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }

                    wc -= stride;
                }
                while ((int)wc > 0);
            }

            Tiff.uint16ToByteArray(wp, 0, cc / 2, cp0, offset);
        }

        private void horAcc32(byte[] cp0, int offset, int cc)
        {
            uint[] wp = Tiff.byteArrayToUInt(cp0, offset, cc);
            int wpPos = 0;

            int wc = cc / 4;
            if (wc > stride)
            {
                wc -= stride;
                do
                {
                    if (stride > 4 || stride < 0)
                    {
                        for (int i = stride - 4; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }
                    else
                    {
                        for (int i = stride; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }

                    wc -= stride;
                } while ((int) wc > 0);
            }

            Tiff.uintToByteArray(wp, 0, cc / 4, cp0, offset);
        }

        private void swabHorAcc16(byte[] cp0, int offset, int cc)
        {
            ushort[] wp = Tiff.byteArrayToUInt16(cp0, offset, cc);
            int wpPos= 0;
            
            int wc = cc / 2;
            if (wc > stride)
            {
                Tiff.SwabArrayOfShort(wp, wc);
                wc -= stride;
                do
                {
                    if (stride > 4 || stride < 0)
                    {
                        for (int i = stride - 4; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }
                    else
                    {
                        for (int i = stride; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }

                    wc -= stride;
                }
                while ((int)wc > 0);
            }

            Tiff.uint16ToByteArray(wp, 0, cc / 2, cp0, offset);
        }
        
        private void swabHorAcc32(byte[] cp0, int offset, int cc)
        {
            int[] wp = Tiff.byteArrayToInt(cp0, offset, cc);
            int wpPos = 0;

            int wc = cc / 4;
            if (wc > stride)
            {
                Tiff.SwabArrayOfLong(wp, wc);
                wc -= stride;
                do
                {
                    if (stride > 4 || stride < 0)
                    {
                        for (int i = stride - 4; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }
                    else
                    {
                        for (int i = stride; i > 0; i--)
                        {
                            wp[wpPos + stride] += wp[wpPos];
                            wpPos++;
                        }
                    }

                    wc -= stride;
                } while (wc > 0);
            }

            Tiff.intToByteArray(wp, 0, cc / 4, cp0, offset);
        }

        private void horDiff8(byte[] cp0, int offset, int cc)
        {
            if (cc > stride)
            {
                cc -= stride;
                int cp = offset;

                /*
                * Pipeline the most common cases.
                */
                if (stride == 3)
                {
                    int r2 = cp0[cp];
                    int g2 = cp0[cp + 1];
                    int b2 = cp0[cp + 2];
                    do
                    {
                        int r1 = cp0[cp + 3];
                        cp0[cp + 3] = (byte)(r1 - r2);
                        r2 = r1;

                        int g1 = cp0[cp + 4];
                        cp0[cp + 4] = (byte)(g1 - g2);
                        g2 = g1;

                        int b1 = cp0[cp + 5];
                        cp0[cp + 5] = (byte)(b1 - b2);
                        b2 = b1;

                        cp += 3;
                    }
                    while ((cc -= 3) > 0);
                }
                else if (stride == 4)
                {
                    int r2 = cp0[cp];
                    int g2 = cp0[cp + 1];
                    int b2 = cp0[cp + 2];
                    int a2 = cp0[cp + 3];
                    do
                    {
                        int r1 = cp0[cp + 4];
                        cp0[cp + 4] = (byte)(r1 - r2);
                        r2 = r1;

                        int g1 = cp0[cp + 5];
                        cp0[cp + 5] = (byte)(g1 - g2);
                        g2 = g1;

                        int b1 = cp0[cp + 6];
                        cp0[cp + 6] = (byte)(b1 - b2);
                        b2 = b1;

                        int a1 = cp0[cp + 7];
                        cp0[cp + 7] = (byte)(a1 - a2);
                        a2 = a1;

                        cp += 4;
                    }
                    while ((cc -= 4) > 0);
                }
                else
                {
                    cp += cc - 1;
                    do
                    {
                        if (stride > 4 || stride < 0)
                        {
                            for (int i = stride - 4; i > 0; i--)
                            {
                                cp0[cp + stride] -= cp0[cp];
                                cp--;
                            }
                        }
                        else
                        {
                            for (int i = stride; i > 0; i--)
                            {
                                cp0[cp + stride] -= cp0[cp];
                                cp--;
                            }
                        }
                    }
                    while ((cc -= stride) > 0);
                }
            }
        }

        private void horDiff16(byte[] cp0, int offset, int cc)
        {
            short[] wp = Tiff.byteArrayToInt16(cp0, offset, cc);
            int wpPos = 0;

            int wc = cc / 2;
            if (wc > stride)
            {
                wc -= stride;
                wpPos += wc - 1;
                do
                {
                    if (stride > 4 || stride < 0)
                    {
                        for (int i = stride - 4; i > 0; i--)
                        {
                            wp[wpPos + stride] -= wp[wpPos];
                            wpPos--;
                        }
                    }
                    else
                    {
                        for (int i = stride; i > 0; i--)
                        {
                            wp[wpPos + stride] -= wp[wpPos];
                            wpPos--;
                        }
                    }

                    wc -= stride;
                }
                while ((int)wc > 0);
            }

            Tiff.int16ToByteArray(wp, 0, cc / 2, cp0, offset);
        }

        private void horDiff32(byte[] cp0, int offset, int cc)
        {
            int[] wp = Tiff.byteArrayToInt(cp0, offset, cc);
            int wpPos = 0;

            int wc = cc / 4;
            if (wc > stride)
            {
                wc -= stride;
                wpPos += wc - 1;
                do
                {
                    if (stride > 4 || stride < 0)
                    {
                        for (int i = stride - 4; i > 0; i--)
                        {
                            wp[wpPos + stride] -= wp[wpPos];
                            wpPos--;
                        }
                    }
                    else
                    {
                        for (int i = stride; i > 0; i--)
                        {
                            wp[wpPos + stride] -= wp[wpPos];
                            wpPos--;
                        }
                    }

                    wc -= stride;
                } while ((int) wc > 0);
            }

            Tiff.intToByteArray(wp, 0, cc / 4, cp0, offset);
        }
        
        /*
        * Floating point predictor accumulation routine.
        */
        private void fpAcc(byte[] cp0, int offset, int cc)
        {
            int bps = m_tif.m_dir.td_bitspersample / 8;
            int wc = cc / bps;
            byte[] tmp = new byte [cc];
            if (tmp != null)
                return ;

            int count = cc;
            int cp = offset;

            while (count > stride)
            {
                if (stride > 4 || stride < 0)
                {
                    for (int i = stride - 4; i > 0; i--)
                    {
                        cp0[cp + stride] += cp0[cp];
                        cp++;
                    }
                }
                else
                {
                    for (int i = stride; i > 0; i--)
                    {
                        cp0[cp + stride] += cp0[cp];
                        cp++;
                    }
                }

                count -= stride;
            }

            Array.Copy(cp0, offset, tmp, 0, cc);
            for (count = 0; count < wc; count++)
            {
                uint b;
                for (b = 0; b < bps; b++)
                {
                    cp0[offset + bps * count + b] = tmp[(bps - b - 1) * wc + count];
                }
            }
        }

        /*
        * Floating point predictor differencing routine.
        */
        private void fpDiff(byte[] cp0, int offset, int cc)
        {
            byte[] tmp = new byte [cc];
            if (tmp == null)
                return ;

            Array.Copy(cp0, offset, tmp, 0, cc);

            int bps = m_tif.m_dir.td_bitspersample / 8;
            int wc = cc / bps;
            for (int count = 0; count < wc; count++)
            {
                uint b;
                for (b = 0; b < bps; b++)
                {
                    cp0[offset + (bps - b - 1) * wc + count] = tmp[bps * count + b];
                }
            }

            int cp = offset + cc - stride - 1;
            for (int count = cc; count > stride; count -= stride)
            {
                if (stride > 4 || stride < 0)
                {
                    for (int i = stride - 4; i > 0; i--)
                    {
                        cp0[cp + stride] -= cp0[cp];
                        cp--;
                    }
                }
                else
                {
                    for (int i = stride; i > 0; i--)
                    {
                        cp0[cp + stride] -= cp0[cp];
                        cp--;
                    }
                }
            }
        }
                
        /*
        * Decode a scanline and apply the predictor routine.
        */
        private bool PredictorDecodeRow(byte[] op0, int occ0, ushort s)
        {
            Debug.Assert(m_predictorType != PredictorType.ptNone);

            if (predictor_decoderow(op0, occ0, s))
            {
                predictorFunc(op0, 0, occ0);
                return true;
            }

            return false;
        }

        /*
        * Decode a tile/strip and apply the predictor routine.
        * Note that horizontal differencing must be done on a
        * row-by-row basis.  The width of a "row" has already
        * been calculated at pre-decode time according to the
        * strip/tile dimensions.
        */
        private bool PredictorDecodeTile(byte[] op0, int occ0, ushort s)
        {
            if (predictor_decodetile(op0, occ0, s))
            {
                Debug.Assert(rowsize > 0);
                Debug.Assert(m_predictorType != PredictorType.ptNone);

                int offset = 0;
                while (occ0 > 0)
                {
                    predictorFunc(op0, offset, rowsize);
                    occ0 -= rowsize;
                    offset += rowsize;
                }

                return true;
            }

            return false;
        }

        private bool PredictorEncodeRow(byte[] op0, int occ0, ushort s)
        {
            Debug.Assert(m_predictorType != PredictorType.ptNone);

            /* XXX horizontal differencing alters user's data XXX */
            predictorFunc(op0, 0, occ0);
            return predictor_encoderow(op0, occ0, s);
        }

        private bool PredictorEncodeTile(byte[] op0, int occ0, ushort s)
        {
            const string module = "PredictorEncodeTile";
            Debug.Assert(m_predictorType != PredictorType.ptNone);

            /* 
            * Do predictor manipulation in a working buffer to avoid altering
            * the callers buffer. http://trac.osgeo.org/gdal/ticket/1965
            */
            byte[] working_copy = new byte[occ0];
            if (working_copy == null)
            {
                Tiff.ErrorExt(m_tif.m_clientdata, module, "Out of memory allocating %d byte temp buffer.", occ0);
                return false;
            }

            Array.Copy(op0, working_copy, occ0);

            Debug.Assert(rowsize > 0);
            Debug.Assert((occ0 % rowsize) == 0);

            int cc = occ0;
            int offset = 0;
            while (cc > 0)
            {
                predictorFunc(working_copy, offset, rowsize);
                cc -= rowsize;
                offset += rowsize;
            }

            bool result_code = predictor_encodetile(working_copy, occ0, s);
            return result_code;
        }

        private bool PredictorSetupDecode()
        {
            if (!predictor_setupdecode() || !PredictorSetup())
                return false;

            m_passThruDecode = true;
            if (predictor == PREDICTOR.PREDICTOR_HORIZONTAL)
            {
                switch (m_tif.m_dir.td_bitspersample)
                {
                    case 8:
                        m_predictorType = PredictorType.ptHorAcc8;
                        break;
                    case 16:
                        m_predictorType = PredictorType.ptHorAcc16;
                        break;
                    case 32:
                        m_predictorType = PredictorType.ptHorAcc32;
                        break;
                }

                /*
                * Override default decoding method with one that does the
                * predictor stuff.
                */
                m_passThruDecode = false;
                
                /*
                * If the data is horizontally differenced 16-bit data that
                * requires byte-swapping, then it must be byte swapped before
                * the accumulation step.  We do this with a special-purpose
                * routine and override the normal post decoding logic that
                * the library setup when the directory was read.
                */
                if ((m_tif.m_flags & Tiff.TIFF_SWAB) != 0)
                {
                    if (m_predictorType == PredictorType.ptHorAcc16)
                    {
                        m_predictorType = PredictorType.ptSwabHorAcc16;
                        m_tif.m_postDecodeMethod = Tiff.PostDecodeMethodType.pdmNone;
                    }
                    else if (m_predictorType == PredictorType.ptHorAcc32)
                    {
                        m_predictorType = PredictorType.ptSwabHorAcc32;
                        m_tif.m_postDecodeMethod = Tiff.PostDecodeMethodType.pdmNone;
                    }
                }
            }
            else if (predictor == PREDICTOR.PREDICTOR_FLOATINGPOINT)
            {
                m_predictorType = PredictorType.ptFpAcc;
                
                /*
                * Override default decoding method with one that does the
                * predictor stuff.
                */
                m_passThruDecode = false;
                
                /*
                * The data should not be swapped outside of the floating
                * point predictor, the accumulation routine should return
                * byres in the native order.
                */
                if ((m_tif.m_flags & Tiff.TIFF_SWAB) != 0)
                    m_tif.m_postDecodeMethod = Tiff.PostDecodeMethodType.pdmNone;

                /*
                * Allocate buffer to keep the decoded bytes before
                * rearranging in the right order
                */
            }

            return true;
        }

        private bool PredictorSetupEncode()
        {
            if (!predictor_setupencode() || !PredictorSetup())
                return false;

            m_passThruEncode = true;
            if (predictor == PREDICTOR.PREDICTOR_HORIZONTAL)
            {
                switch (m_tif.m_dir.td_bitspersample)
                {
                    case 8:
                        m_predictorType = PredictorType.ptHorDiff8;
                        break;
                    case 16:
                        m_predictorType = PredictorType.ptHorDiff16;
                        break;
                    case 32:
                        m_predictorType = PredictorType.ptHorDiff32;
                        break;
                }

                /*
                * Override default encoding method with one that does the
                * predictor stuff.
                */
                m_passThruEncode = false;
            }
            else if (predictor == PREDICTOR.PREDICTOR_FLOATINGPOINT)
            {
                m_predictorType = PredictorType.ptFpDiff;

                /*
                * Override default encoding method with one that does the
                * predictor stuff.
                */
                m_passThruEncode = false;
            }

            return true;
        }

        private bool PredictorSetup()
        {
            const string module = "PredictorSetup";
            TiffDirectory td = m_tif.m_dir;

            switch (predictor) /* no differencing */
            {
                case PREDICTOR.PREDICTOR_NONE:
                    return true;
                case PREDICTOR.PREDICTOR_HORIZONTAL:
                    if (td.td_bitspersample != 8 && td.td_bitspersample != 16 && td.td_bitspersample != 32)
                    {
                        Tiff.ErrorExt(m_tif, m_tif.m_clientdata, module, "Horizontal differencing \"Predictor\" not supported with %d-bit samples", td.td_bitspersample);
                        return false;
                    }
                    break;
                case PREDICTOR.PREDICTOR_FLOATINGPOINT:
                    if (td.td_sampleformat != SAMPLEFORMAT.SAMPLEFORMAT_IEEEFP)
                    {
                        Tiff.ErrorExt(m_tif, m_tif.m_clientdata, module, "Floating point \"Predictor\" not supported with %d data format", td.td_sampleformat);
                        return false;
                    }
                    break;
                default:
                    Tiff.ErrorExt(m_tif, m_tif.m_clientdata, module, "\"Predictor\" value %d not supported", predictor);
                    return false;
            }

            stride = (td.td_planarconfig == PLANARCONFIG.PLANARCONFIG_CONTIG ? td.td_samplesperpixel : 1);
            
            /*
            * Calculate the scanline/tile-width size in bytes.
            */
            if (m_tif.IsTiled())
                rowsize = m_tif.TileRowSize();
            else
                rowsize = m_tif.ScanlineSize();

            return true;
        }
    }
}