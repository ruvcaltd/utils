import { Directive, ElementRef, Input, OnChanges, SimpleChanges } from '@angular/core';

@Directive({
  selector: '[appNumberFormat]',
  standalone: true
})
export class NumberFormatDirective implements OnChanges {
  @Input('appNumberFormat') numberValue!: number;

  constructor(private el: ElementRef) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['numberValue'] && this.numberValue != null) {
      const formatted = new Intl.NumberFormat().format(this.numberValue);
      this.el.nativeElement.innerText = formatted;
    }
  }
}
