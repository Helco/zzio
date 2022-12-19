using System;
using System.Linq;

namespace zzio.db
{
    public abstract class MappedRow
    {
        protected readonly MappedDB mappedDB;
        protected readonly Row row;

        protected string foreignText(int cellIndex)
        {
            UID foreignUid = row.cells[cellIndex].ForeignKey.uid;
            return mappedDB.GetText(foreignUid).Text;
        }

        protected int[] integerRange(int cellIndex, int count)
        {
            return Enumerable.Range(cellIndex, count)
                .Select(cellI => row.cells[cellI].Integer)
                .ToArray();
        }

        protected MappedRow(ModuleType expectedModule, MappedDB mappedDB, Row row)
        {
            if (row.uid.Module != (int)expectedModule)
                throw new InvalidOperationException("Invalid module type for mapped row");
            this.mappedDB = mappedDB;
            this.row = row;
        }

        public UID Uid => row.uid;

        public ModuleType Module => EnumUtils.intToEnum<ModuleType>(Uid.Module);
    }
}
