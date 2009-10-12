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

using System;
using System.Collections.Generic;
using System.Text;

using thandle_t = System.Object;

namespace BitMiracle.LibTiff
{
    public class TiffStream
    {
        public virtual int Read(thandle_t fd, byte[] buf, int offset, int size)
        {
            DWORD dwSizeRead;
            if (!ReadFile(fd, &buf[offset], size, &dwSizeRead, null))
                return 0;

            return dwSizeRead;
        }

        public virtual int Write(thandle_t fd, byte[] buf, int size)
        {
            DWORD dwSizeWritten;
            if (!WriteFile(fd, buf, size, &dwSizeWritten, null))
                return 0;

            return dwSizeWritten;
        }

        public virtual uint Seek(thandle_t fd, uint off, int whence)
        {
            /* we use this as a special code, so avoid accepting it */
            if (off == 0xFFFFFFFF)
                return 0xFFFFFFFF;

            DWORD dwMoveMethod = FILE_BEGIN;
            switch (whence)
            {
                case SEEK_SET:
                    dwMoveMethod = FILE_BEGIN;
                    break;
                case SEEK_CUR:
                    dwMoveMethod = FILE_CURRENT;
                    break;
                case SEEK_END:
                    dwMoveMethod = FILE_END;
                    break;
            }

            DWORD dwMoveHigh = 0;
            return SetFilePointer(fd, (LONG)off, (PLONG) & dwMoveHigh, dwMoveMethod);
        }

        public virtual bool Close(thandle_t fd)
        {
            return (CloseHandle(fd) ? true : false);
        }

        public virtual uint Size(thandle_t fd)
        {
            return GetFileSize(fd, null);
        }
    }
}
