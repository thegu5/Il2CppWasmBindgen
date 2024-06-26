using System.Reflection.Metadata;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.Utils;
using Cpp2IL.Core.Utils.AsmResolver;
using WasmDisassembler;
using MethodDefinition = AsmResolver.DotNet.MethodDefinition;

namespace Il2CppWasmBindgen;

// ReSharper disable once InconsistentNaming
public class WasmDirectILOutputFormat : AsmResolverDllOutputFormat
{
    public override string OutputFormatId => "WasmDirectILOutputFormat";
    public override string OutputFormatName => "Wasm Direct IL Conversion Output Format";

    private MethodDefinition currentMethod;
    private ReferenceImporter currentImporter;
    
    protected override void FillMethodBody(MethodDefinition methodDefinition, MethodAnalysisContext methodContext)
    {
        currentMethod = methodDefinition;
        currentImporter = new ReferenceImporter(currentMethod.Module);
        
        var wasmdef = WasmUtils.TryGetWasmDefinition(methodContext.Definition);
        if (wasmdef is null) return;
        try
        {
            var wasminstrs = Disassembler.Disassemble(wasmdef.AssociatedFunctionBody?.Instructions,
                (uint)methodContext.UnderlyingPointer);

            methodDefinition.CilMethodBody = new CilMethodBody(methodDefinition);
            methodDefinition.CilMethodBody.Instructions.Clear();
            methodDefinition.CilMethodBody.Instructions.AddRange(wasminstrs.SelectMany(ProcessInstruction));
        }
        catch (Exception e)
        {
            Console.WriteLine("Disassembly failed for method " + methodContext.MethodName +" with reason " + e.Message);
        }
    }

    private List<CilInstruction> ProcessInstruction(WasmInstruction instr)
    {
        switch (instr.Mnemonic)
        {
            case WasmMnemonic.Unreachable: // TODO: throw a System.Diagnostics.UnreachableException
                //return [new CilInstruction(CilOpCodes.Newobj)]
                return [new CilInstruction(CilOpCodes.Nop)];
            case WasmMnemonic.Nop:
                return [new CilInstruction(CilOpCodes.Nop)];
            case WasmMnemonic.Block:
                break;
            case WasmMnemonic.Loop:
                break;
            case WasmMnemonic.If:
                break;
            case WasmMnemonic.Else:
                break;
            case WasmMnemonic.Proposed_Try:
                break;
            case WasmMnemonic.Proposed_Catch:
                break;
            case WasmMnemonic.Proposed_Throw:
                break;
            case WasmMnemonic.Proposed_Rethrow:
                break;
            case WasmMnemonic.Proposed_BrOnExn:
                break;
            case WasmMnemonic.End:
                break;
            case WasmMnemonic.Br:
                break;
            case WasmMnemonic.BrIf:
                break;
            case WasmMnemonic.BrTable:
                break;
            case WasmMnemonic.Return:
                return [new CilInstruction(CilOpCodes.Ret)];
            case WasmMnemonic.Call:
                // cursed
                var defs = WasmUtils.GetMethodDefinitionsAtIndex((int)(ulong)instr.Operands[0]);
                if (defs is null) break;
                if (defs.Count > 1)
                    Console.WriteLine(
                        $"DEBUG: Method index {instr.Operands[0]} has more than one managed method inside of it");
                var imported = currentMethod.Module.CorLibTypeFactory.CorLibScope
                    .CreateTypeReference(defs[0].DeclaringType.Namespace, defs[0].DeclaringType.Name)
                    .CreateMemberReference(defs[0].Name,
                        defs[0].IsStatic
                            ? MethodSignature.CreateStatic(
                                AsmResolverUtils.GetTypeSignatureFromIl2CppType(currentMethod.Module,
                                    defs[0].RawReturnType), defs[0].GenericContainer?.genericParameterCount ?? 0, defs[0].Parameters.Select(p => AsmResolverUtils.GetTypeSignatureFromIl2CppType(currentMethod.Module, p.RawType)))
                            : MethodSignature.CreateInstance(
                                AsmResolverUtils.GetTypeSignatureFromIl2CppType(currentMethod.Module,
                                    defs[0].RawReturnType), defs[0].GenericContainer?.genericParameterCount ?? 0, defs[0].Parameters.Select(p => AsmResolverUtils.GetTypeSignatureFromIl2CppType(currentMethod.Module, p.RawType))));
                Console.WriteLine(currentMethod.Name + " is calling " + defs[0].Name);
                return [new CilInstruction(CilOpCodes.Call, imported)];
            case WasmMnemonic.CallIndirect:
                break;
            case WasmMnemonic.Proposed_ReturnCall:
                break;
            case WasmMnemonic.Proposed_ReturnCallIndirect:
                break;
            case WasmMnemonic.Reserved_14:
                break;
            case WasmMnemonic.Reserved_15:
                break;
            case WasmMnemonic.Reserved_16:
                break;
            case WasmMnemonic.Reserved_17:
                break;
            case WasmMnemonic.Reserved_18:
                break;
            case WasmMnemonic.Reserved_19:
                break;
            case WasmMnemonic.Drop:
                return [new CilInstruction(CilOpCodes.Pop)];
            case WasmMnemonic.Select:
                break;
            case WasmMnemonic.Proposed_SelectT:
                break;
            case WasmMnemonic.Reserved_1D:
                break;
            case WasmMnemonic.Reserved_1E:
                break;
            case WasmMnemonic.Reserved_1F:
                break; 
            case WasmMnemonic.LocalGet:// TODO: local variables do not work with these
                return [new CilInstruction(CilOpCodes.Ldarg, (int)(byte)instr.Operands[0])]; // todo: optimized form _0, _1
            case WasmMnemonic.LocalSet:
                return [new CilInstruction(CilOpCodes.Starg, (int)(byte)instr.Operands[0])];
            case WasmMnemonic.LocalTee:
                return
                [
                    new CilInstruction(CilOpCodes.Dup),
                    new CilInstruction(CilOpCodes.Starg, (int)(byte)instr.Operands[0])
                ];
            case WasmMnemonic.GlobalGet:
                break;
            case WasmMnemonic.GlobalSet:
                break;
            case WasmMnemonic.Proposed_TableGet:
                break;
            case WasmMnemonic.Proposed_TableSet:
                break;
            case WasmMnemonic.Reserved_27:
                break;
            case WasmMnemonic.I32Load8_S:
                break;
            case WasmMnemonic.I32Load8_U:
                break;
            case WasmMnemonic.I32Load16_S:
                break;
            case WasmMnemonic.I32Load16_U:
                break;
            case WasmMnemonic.I64Load8_S:
                break;
            case WasmMnemonic.I64Load8_U:
                break;
            case WasmMnemonic.I64Load16_S:
                break;
            case WasmMnemonic.I64Load16_U:
                break;
            case WasmMnemonic.I64Load32_S:
                break;
            case WasmMnemonic.I64Load32_U:
                break;
            case WasmMnemonic.I32Store:
                break;
            case WasmMnemonic.I64Store:
                break;
            case WasmMnemonic.F32Store:
                break;
            case WasmMnemonic.F64Store:
                break;
            case WasmMnemonic.I32Store8:
                break;
            case WasmMnemonic.I32Store16:
                break;
            case WasmMnemonic.I64Store8:
                break;
            case WasmMnemonic.I64Store16:
                break;
            case WasmMnemonic.I64Store32:
                break;
            case WasmMnemonic.MemorySize:
                break;
            case WasmMnemonic.MemoryGrow:
                break;
            case WasmMnemonic.I32Const:
                return [CilInstruction.CreateLdcI4((int)(ulong)instr.Operands[0])];
            case WasmMnemonic.I64Const:
                return [new CilInstruction(CilOpCodes.Ldc_I8, (long)(ulong)instr.Operands[0])];
            case WasmMnemonic.F32Const:
                return [new CilInstruction(CilOpCodes.Ldc_R4, instr.Operands[0])];
            case WasmMnemonic.F64Const:
                return [new CilInstruction(CilOpCodes.Ldc_R8, instr.Operands[0])];
            // numerics
            case WasmMnemonic.I32Load: // TODO
                break;
            case WasmMnemonic.I64Load:
                break;
            case WasmMnemonic.F32Load:
                break;
            case WasmMnemonic.F64Load:
                break;
            case WasmMnemonic.I32Eqz:
            case WasmMnemonic.I64Eqz: // todo: make sure not conving is valid
                return
                [
                    new CilInstruction(CilOpCodes.Ldc_I4_0),
                    new CilInstruction(CilOpCodes.Ceq)
                ];
            case WasmMnemonic.I32Eq:
            case WasmMnemonic.I64Eq:
            case WasmMnemonic.F32Eq:
            case WasmMnemonic.F64Eq:
                return [new CilInstruction(CilOpCodes.Ceq)];
            case WasmMnemonic.I32Ne:
            case WasmMnemonic.I64Ne:
            case WasmMnemonic.F32Ne:
            case WasmMnemonic.F64Ne:
                return
                [
                    new CilInstruction(CilOpCodes.Ceq),
                    new CilInstruction(CilOpCodes.Ldc_I4_0),
                    new CilInstruction(CilOpCodes.Ceq)
                ];
            case WasmMnemonic.I32Lt_S:
            case WasmMnemonic.I64Lt_S:
            case WasmMnemonic.F32Lt:
            case WasmMnemonic.F64Lt:
                return [new CilInstruction(CilOpCodes.Clt)];
            case WasmMnemonic.I32Lt_U:
            case WasmMnemonic.I64Lt_U:
                return [new CilInstruction(CilOpCodes.Clt_Un)];
            case WasmMnemonic.I32Gt_S:
            case WasmMnemonic.I64Gt_S:
            case WasmMnemonic.F32Gt:
            case WasmMnemonic.F64Gt:
                return [new CilInstruction(CilOpCodes.Cgt)];
            case WasmMnemonic.I32Gt_U:
            case WasmMnemonic.I64Gt_U:
                return [new CilInstruction(CilOpCodes.Cgt_Un)];
            case WasmMnemonic.I32Le_S:
            case WasmMnemonic.I64Le_S:
            case WasmMnemonic.F32Le: // TODO: figure out why compiler prefers clt.un even though floats are signed
            case WasmMnemonic.F64Le:
                return
                [
                    new CilInstruction(CilOpCodes.Cgt),
                    new CilInstruction(CilOpCodes.Ldc_I4_0),
                    new CilInstruction(CilOpCodes.Ceq)
                ];
            case WasmMnemonic.I32Le_U:
            case WasmMnemonic.I64Le_U:
                return
                [
                    new CilInstruction(CilOpCodes.Cgt_Un),
                    new CilInstruction(CilOpCodes.Ldc_I4_0),
                    new CilInstruction(CilOpCodes.Ceq)
                ];
            case WasmMnemonic.I32Ge_S:
            case WasmMnemonic.I64Ge_S:
            case WasmMnemonic.F32Ge: // see above comment
            case WasmMnemonic.F64Ge:
                return
                [
                    new CilInstruction(CilOpCodes.Clt),
                    new CilInstruction(CilOpCodes.Ldc_I4_0),
                    new CilInstruction(CilOpCodes.Ceq)
                ];
            case WasmMnemonic.I32Ge_U:
            case WasmMnemonic.I64Ge_U:
                return
                [
                    new CilInstruction(CilOpCodes.Clt_Un),
                    new CilInstruction(CilOpCodes.Ldc_I4_0),
                    new CilInstruction(CilOpCodes.Ceq)
                ];
            case WasmMnemonic.I32Clz: // TODO
                break;
            case WasmMnemonic.I32Ctz:
                break;
            case WasmMnemonic.I32PopCnt:
                break;
            case WasmMnemonic.I32Add:
            case WasmMnemonic.I64Add:
            case WasmMnemonic.F32Add:
            case WasmMnemonic.F64Add:
                return [new CilInstruction(CilOpCodes.Add)];
            case WasmMnemonic.I32Sub:
            case WasmMnemonic.I64Sub:
            case WasmMnemonic.F32Sub:
            case WasmMnemonic.F64Sub:
                return [new CilInstruction(CilOpCodes.Sub)];
            case WasmMnemonic.I32Mul:
            case WasmMnemonic.I64Mul:
            case WasmMnemonic.F32Mul:
            case WasmMnemonic.F64Mul:
                return [new CilInstruction(CilOpCodes.Mul)];
            case WasmMnemonic.I32Div_S:
            case WasmMnemonic.I64Div_S:
            case WasmMnemonic.F32Div:
            case WasmMnemonic.F64Div:
                return [new CilInstruction(CilOpCodes.Div)];
            case WasmMnemonic.I32Div_U:
            case WasmMnemonic.I64Div_U:
                return [new CilInstruction(CilOpCodes.Div_Un)];
            case WasmMnemonic.I32Rem_S:
            case WasmMnemonic.I64Rem_S:
                return [new CilInstruction(CilOpCodes.Rem)];
            case WasmMnemonic.I32Rem_U:
            case WasmMnemonic.I64Rem_U:
                return [new CilInstruction(CilOpCodes.Rem_Un)];
            case WasmMnemonic.I32And:
            case WasmMnemonic.I64And:
                return [new CilInstruction(CilOpCodes.And)];
            case WasmMnemonic.I32Or:
            case WasmMnemonic.I64Or:
                return [new CilInstruction(CilOpCodes.Or)];
            case WasmMnemonic.I32Xor:
            case WasmMnemonic.I64Xor:
                return [new CilInstruction(CilOpCodes.Xor)];
            case WasmMnemonic.I32Shl:
            case WasmMnemonic.I64Shl:
                return [new CilInstruction(CilOpCodes.Shl)];
            case WasmMnemonic.I32Shr_S:
            case WasmMnemonic.I64Shr_S:
                return [new CilInstruction(CilOpCodes.Shr)];
            case WasmMnemonic.I32Shr_U:
            case WasmMnemonic.I64Shr_U:
                return [new CilInstruction(CilOpCodes.Shr_Un)];
            case WasmMnemonic.I32Rotl:
                break;
            case WasmMnemonic.I32Rotr:
                break;
            case WasmMnemonic.I64Clz:
                break;
            case WasmMnemonic.I64Ctz:
                break;
            case WasmMnemonic.I64PopCnt:
                break;
            case WasmMnemonic.I64Rotl:
                break;
            case WasmMnemonic.I64Rotr:
                break;
            case WasmMnemonic.F32Abs:
                break;
            case WasmMnemonic.F32Neg:
                break;
            case WasmMnemonic.F32Ceil:
                break;
            case WasmMnemonic.F32Floor:
                break;
            case WasmMnemonic.F32Trunc:
                break;
            case WasmMnemonic.F32Nearest:
                break;
            case WasmMnemonic.F32Sqrt:
                break;
            case WasmMnemonic.F32Min:
                break;
            case WasmMnemonic.F32Max:
                break;
            case WasmMnemonic.F32Copysign:
                break;
            case WasmMnemonic.F64Abs:
                break;
            case WasmMnemonic.F64Neg:
                break;
            case WasmMnemonic.F64Ceil:
                break;
            case WasmMnemonic.F64Floor:
                break;
            case WasmMnemonic.F64Trunc:
                break;
            case WasmMnemonic.F64Nearest:
                break;
            case WasmMnemonic.F64Sqrt:
                break;
            case WasmMnemonic.F64Min:
                break;
            case WasmMnemonic.F64Max:
                break;
            case WasmMnemonic.F64Copysign:
                break;
            case WasmMnemonic.I32Wrap_I64:
                break;
            case WasmMnemonic.I32Trunc_F32_S:
                break;
            case WasmMnemonic.I32Trunc_F32_U:
                break;
            case WasmMnemonic.I32Trunc_F64_S:
                break;
            case WasmMnemonic.I32Trunc_F64_U:
                break;
            case WasmMnemonic.I64Extend_I32_S:
                break;
            case WasmMnemonic.I64Extend_I32_U:
                break;
            case WasmMnemonic.I64Trunc_F32_S:
                break;
            case WasmMnemonic.I64Trunc_F32_U:
                break;
            case WasmMnemonic.I64Trunc_F64_S:
                break;
            case WasmMnemonic.I64Trunc_F64_U:
                break;
            case WasmMnemonic.F32Convert_I32_S:
                break;
            case WasmMnemonic.F32Convert_I32_U:
                break;
            case WasmMnemonic.F32Convert_I64_S:
                break;
            case WasmMnemonic.F32Convert_I64_U:
                break;
            case WasmMnemonic.F32Demote_F64:
                break;
            case WasmMnemonic.F64Convert_I32_S:
                break;
            case WasmMnemonic.F64Convert_I32_U:
                break;
            case WasmMnemonic.F64Convert_I64_S:
                break;
            case WasmMnemonic.F64Convert_I64_U:
                break;
            case WasmMnemonic.F64Promote_F32:
                break;
            case WasmMnemonic.I32Reinterpret_F32:
                break;
            case WasmMnemonic.I64Reinterpret_F64:
                break;
            case WasmMnemonic.F32Reinterpret_I32:
                break;
            case WasmMnemonic.F64Reinterpret_I64:
                break;
            case WasmMnemonic.Proposed_I32Extend8_S:
                break;
            case WasmMnemonic.Proposed_I32Extend16_S:
                break;
            case WasmMnemonic.Proposed_I64Extend8_S:
                break;
            case WasmMnemonic.Proposed_I64Extend16_S:
                break;
            case WasmMnemonic.Proposed_I64Extend32_S:
                break;
            case WasmMnemonic.Proposed_RefNull:
                break;
            case WasmMnemonic.Proposed_RefIsNull:
                break;
            case WasmMnemonic.Proposed_RefFunc:
                break;
            case WasmMnemonic.Proposed_FC_Extensions:
                break;
            case WasmMnemonic.Proposed_SIMD:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        return [];
    }
}