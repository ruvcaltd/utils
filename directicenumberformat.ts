import { Directive, ElementRef, forwardRef, HostListener, Input } from '@angular/core';
import { NG_VALUE_ACCESSOR, ControlValueAccessor } from '@angular/forms';

@Directive({
  selector: '[appNumberFormat]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => NumberFormatDirective),
      multi: true
    }
  ]
})
export class NumberFormatDirective implements ControlValueAccessor {
  private onChange = (value: any) => {};
  private onTouched = () => {};

  @Input() locale: string = 'en-US';

  constructor(private el: ElementRef<HTMLInputElement>) {}

  @HostListener('input', ['$event.target.value'])
  onInput(rawValue: string) {
    const parsed = Number(rawValue.replace(/[^\d.-]/g, ''));
    this.onChange(parsed);
  }

  @HostListener('blur')
  onBlur() {
    this.onTouched();
    const value = this.el.nativeElement.value;
    const parsed = Number(value.replace(/[^\d.-]/g, ''));
    this.el.nativeElement.value = this.format(parsed);
  }

  writeValue(value: any): void {
    if (value !== null && value !== undefined) {
      this.el.nativeElement.value = this.format(value);
    } else {
      this.el.nativeElement.value = '';
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.el.nativeElement.disabled = isDisabled;
  }

  private format(value: number): string {
    return new Intl.NumberFormat(this.locale).format(value);
  }
}
