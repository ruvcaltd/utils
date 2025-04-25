// number-format.directive.ts
import { Directive, ElementRef, Input, OnInit } from '@angular/core';
import { NgIf } from '@angular/common';

@Directive({
  standalone: true,
  selector: '[appNumberFormat]',
  hostDirectives: [NgIf],
})
export class NumberFormatDirective implements OnInit {
  @Input('appNumberFormat') numberValue!: number;

  constructor(private el: ElementRef) {}

  ngOnInit() {
    const formatted = new Intl.NumberFormat().format(this.numberValue);
    this.el.nativeElement.innerText = formatted;
  }
}
