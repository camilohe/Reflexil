﻿/* Reflexil Copyright (c) 2007-2014 Sebastien LEBRETON

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. */

using System;
using System.Collections;
using System.Linq;
using Mono.Cecil;
using Reflector.CodeModel;
using Reflexil.Utils;

namespace Reflexil.Plugins.Reflector
{

	internal class ReflectorPlugin : BasePlugin
	{

		public override string HostApplication
		{
			get { return "Reflector"; }
		}

		public ReflectorPlugin(IPackage package) : base(package)
		{
		}

		public override bool IsAssemblyNameReferenceHandled(object item)
		{
			var anref = item as IAssemblyReference;
			return anref != null && anref.Context != null;
		}

		public override bool IsAssemblyDefinitionHandled(object item)
		{
			var asm = item as IAssembly;
			return asm != null && asm.Type != AssemblyType.None;
		}

		public override bool IsTypeDefinitionHandled(object item)
		{
			return item is ITypeDeclaration;
		}

		public override bool IsModuleDefinitionHandled(object item)
		{
			return item is IModule;
		}

		public override bool IsMethodDefinitionHandled(object item)
		{
			return item is IMethodDeclaration;
		}

		public override bool IsPropertyDefinitionHandled(object item)
		{
			return item is IPropertyDeclaration;
		}

		public override bool IsFieldDefinitionHandled(object item)
		{
			return item is IFieldDeclaration;
		}

		public override bool IsEventDefinitionHandled(object item)
		{
			return item is IEventDeclaration;
		}

		public override bool IsEmbeddedResourceHandled(object item)
		{
			return item is IEmbeddedResource;
		}

		public override bool IsAssemblyLinkedResourceHandled(object item)
		{
			return item is IResource && !IsEmbeddedResourceHandled(item) && !IsLinkedResourceHandled(item);
		}

		public override bool IsLinkedResourceHandled(object item)
		{
			return item is IFileResource;
		}

		public override AssemblyDefinition GetAssemblyDefinition(object item)
		{
			var aloc = item as IAssemblyLocation;
			if (aloc == null)
				return null;

			var context = PluginFactory.GetInstance().GetAssemblyContext(aloc.Location);
			return context != null ? context.AssemblyDefinition : null;
		}

		private static IModule GetModule(ITypeReference itype)
		{
			if ((itype.Owner) is IModule)
				return ((IModule) itype.Owner);

			if ((itype.Owner) is ITypeReference)
				return GetModule((ITypeReference) itype.Owner);

			return null;
		}

		public override MethodDefinition GetMethodDefinition(object item)
		{
			return GetDefinitionFromDeclaration<MethodDefinition, IMethodDeclaration>(item, mdec => GetModule(mdec.DeclaringType as ITypeDeclaration) , (context, mdec) => context.GetMethodDefinition(mdec));
		}

		public override AssemblyNameReference GetAssemblyNameReference(object item)
		{
			return GetDefinitionFromDeclaration<AssemblyNameReference, IAssemblyReference>(item, anref => anref.Context, (context, anref) => context.GetAssemblyNameReference(anref));
		}

		public override PropertyDefinition GetPropertyDefinition(object item)
		{
			return GetDefinitionFromDeclaration<PropertyDefinition, IPropertyDeclaration>(item, pdec => GetModule(pdec.DeclaringType as ITypeDeclaration), (context, pdec) => context.GetPropertyDefinition(pdec));
		}

		public override FieldDefinition GetFieldDefinition(object item)
		{
			return GetDefinitionFromDeclaration<FieldDefinition, IFieldDeclaration>(item, fdec => GetModule(fdec.DeclaringType as ITypeDeclaration), (context, fdec) => context.GetFieldDefinition(fdec));
		}

		public override EventDefinition GetEventDefinition(object item)
		{
			return GetDefinitionFromDeclaration<EventDefinition, IEventDeclaration>(item, edec => GetModule(edec.DeclaringType as ITypeDeclaration), (context, edec) => context.GetEventDefinition(edec));
		}

		public override AssemblyLinkedResource GetAssemblyLinkedResource(object item)
		{
			return GetDefinitionFromDeclaration<AssemblyLinkedResource, IResource>(item, res => res.Module, (context, res) => context.GetResource(res) as AssemblyLinkedResource);
		}

		public override LinkedResource GetLinkedResource(object item)
		{
			return GetDefinitionFromDeclaration<LinkedResource, IResource>(item, res => res.Module, (context, res) => context.GetResource(res) as LinkedResource);
		}

		public override EmbeddedResource GetEmbeddedResource(object item)
		{
			return GetDefinitionFromDeclaration<EmbeddedResource, IResource>(item, res => res.Module, (context, res) => context.GetResource(res) as EmbeddedResource);
		}

		private TDef GetDefinitionFromDeclaration<TDef, TDecl>(object item, Func<TDecl, IModule> getModule, Func<ReflectorAssemblyContext, TDecl, TDef> finder) where TDecl : class where TDef : class
		{
			var decl = item as TDecl;
			if (decl == null)
				return null;

			var module = getModule(decl);
			if (module == null)
				return null;

			var context = GetAssemblyContext(module.Location) as ReflectorAssemblyContext;
			if (context == null)
				return null;

			return finder(context, decl);
		}

		public override TypeDefinition GetTypeDefinition(object item)
		{
			return GetDefinitionFromDeclaration<TypeDefinition, ITypeDeclaration>(item, GetModule, (context, tdec) => context.GetTypeDefinition(tdec));
		}

		public override void SynchronizeAssemblyContexts(ICollection assemblies)
		{
			var locations = Assemblies.Cast<IAssembly>().Select(a => Environment.ExpandEnvironmentVariables(a.Location));

			foreach (var location in Assemblycache.Keys.Where(location => !locations.Contains(location)).ToList())
				Assemblycache.Remove(location);
		}

		public override ICollection GetAssemblies(bool wrap)
		{
			if (!wrap)
				return Assemblies;

			var result = new ArrayList();
			foreach (IAssembly asm in Assemblies)
				result.Add(new ReflectorAssemblyWrapper(asm));

			return result;
		}

		public override ModuleDefinition GetModuleDefinition(object item)
		{
			var location = Environment.ExpandEnvironmentVariables(((IModule) item).Location);
			var context = GetAssemblyContext(location);
			return context.AssemblyDefinition.MainModule;
		}

		public override IAssemblyContext GetAssemblyContext(string location)
		{
			return GetAssemblyContext<ReflectorAssemblyContext>(location);
		}

		public override AssemblyDefinition LoadAssembly(string location, bool loadsymbols)
		{
			var parameters = new ReaderParameters {ReadSymbols = loadsymbols, ReadingMode = ReadingMode.Deferred};
			var resolver = new ReflexilAssemblyResolver();
			try
			{
				return resolver.ReadAssembly(location, parameters);
			}
			catch (Exception)
			{
				// perhaps pdb file is not found, just ignore this time
				if (!loadsymbols)
					throw;

				parameters.ReadSymbols = false;
				return resolver.ReadAssembly(location, parameters);
			}
		}

		public void RemoveFromCache(object item)
		{
			foreach (var ctx in Assemblycache.Values.OfType<ReflectorAssemblyContext>())
				ctx.RemoveFromCache(item);
		}

	}
}