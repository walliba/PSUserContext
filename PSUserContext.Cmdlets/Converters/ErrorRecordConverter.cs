using System;
using System.Management.Automation;

namespace PSUserContext.Cmdlets.Converters;

public class ErrorRecordConverter : PSTypeConverter
{
    public override bool CanConvertFrom(object sourceValue, Type destinationType)
    {
        if (sourceValue is PSObject psObject)
        {
            return psObject.TypeNames.Contains("Deserialized.System.Management.Automation.ErrorRecord");
        }

        return false;
    }

    public override bool CanConvertTo(object sourceValue, Type destinationType)
    {
        return this.CanConvertFrom(sourceValue, destinationType) && destinationType == typeof(ErrorRecord);
    }

    public override object ConvertFrom(object sourceValue, Type destinationType, IFormatProvider formatProvider, bool ignoreCase)
    {
        throw new NotImplementedException();
    }

    public override object ConvertTo(object sourceValue, Type destinationType, IFormatProvider formatProvider, bool ignoreCase)
    {
        throw new NotImplementedException();
    }
}