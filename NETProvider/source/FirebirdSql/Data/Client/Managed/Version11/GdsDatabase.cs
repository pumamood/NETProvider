/*
 *	Firebird ADO.NET Data provider for .NET and Mono 
 * 
 *	   The contents of this file are subject to the Initial 
 *	   Developer's Public License Version 1.0 (the "License"); 
 *	   you may not use this file except in compliance with the 
 *	   License. You may obtain a copy of the License at 
 *	   http://www.firebirdsql.org/index.php?op=doc&id=idpl
 *
 *	   Software distributed under the License is distributed on 
 *	   an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either 
 *	   express or implied. See the License for the specific 
 *	   language governing rights and limitations under the License.
 * 
 *	Copyright (c) 2002 - 2007 Carlos Guzman Alvarez
 *	Copyright (c) 2007 - 2008 Jiri Cincura (jiri@cincura.net) 
 *	 
 *  Contributors:
 *      Vladimir Bodecek
 *      
 *	All Rights Reserved.
 */

using System;
using System.Collections;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Net;
using System.Collections.Generic;

using FirebirdSql.Data.Common;

namespace FirebirdSql.Data.Client.Managed.Version11
{
    internal class GdsDatabase : FirebirdSql.Data.Client.Managed.Version10.GdsDatabase
    {
        #region � Constructors �

        public GdsDatabase(FirebirdSql.Data.Client.Managed.Version10.GdsConnection connection)
            : base(connection)
        {
            this.DefferedPacketsProcessing = new List<Action<IResponse>>();
        }

        #endregion

        #region Properties
        public List<Action<IResponse>> DefferedPacketsProcessing { get; private set; }
        #endregion

        #region � Override Statement Creation Methods �

        public override StatementBase CreateStatement()
        {
            return new GdsStatement(this);
        }

        public override StatementBase CreateStatement(ITransaction transaction)
        {
            return new GdsStatement(this, transaction);
        }

        #endregion

        #region Trusted Auth
        public override void AttachWithTrustedAuth(DatabaseParameterBuffer dpb, string dataSource, int port, string database)
        {
            lock (this.SyncObject)
            {
                try
                {
                    using (SSPIHelper sspiHelper = new SSPIHelper())
                    {
                        byte[] authData = sspiHelper.InitializeClientSecurity();
                        dpb.Append(IscCodes.isc_dpb_trusted_auth, authData);

                        // Attach to the database
                        this.Write(IscCodes.op_attach);
                        this.Write((int)0);				    // Database	object ID
                        this.Write(database);				// Database	PATH
                        this.WriteBuffer(dpb.ToArray());	// DPB Parameter buffer
                        this.Flush();

                        IResponse response = this.ReadResponse();
                        while (response is AuthResponse)
                        {
                            authData = sspiHelper.GetClientSecurity(((AuthResponse)response).Data);
                            this.Write(IscCodes.op_trusted_auth);
                            this.WriteBuffer(authData);
                            this.Flush();
                            response = this.ReadResponse();
                        }
                        // Save the database connection handle
                        this.handle = ((GenericResponse)response).ObjectHandle;
                    }
                }
                catch (IOException)
                {
                    try
                    {
                        this.Detach();
                    }
                    catch (Exception)
                    {
                    }

                    throw new IscException(IscCodes.isc_net_write_err);
                }

                // Get server version
                this.serverVersion = this.GetServerVersion();
            }
        }
        #endregion

        #region Public methods
        public override void ReleaseObject(int op, int id)
        {
            lock (this.SyncObject)
            {
                try
                {
                    DoReleaseObjectPacket(op, id);
#warning This isn't in lock anymore later
                    this.DefferedPacketsProcessing.Add(ProcessReleaseObjectResponse);
                }
                catch (IOException)
                {
                    throw new IscException(IscCodes.isc_net_read_err);
                }
            }
        }
        #endregion

        #region Protected methods
        protected override IResponse ReadSingleResponse()
        {
            while (DefferedPacketsProcessing.Count > 0)
            {
                DefferedPacketsProcessing[0](base.ReadSingleResponse());
                DefferedPacketsProcessing.RemoveAt(0);
            }
            return base.ReadSingleResponse();
        }

        protected override IResponse ProcessOperation(int operation)
        {
            switch (operation)
            {
                case IscCodes.op_trusted_auth:
                    return new AuthResponse(this.ReadBuffer());

                default:
                    return base.ProcessOperation(operation);
            }
        }
        #endregion
    }
}